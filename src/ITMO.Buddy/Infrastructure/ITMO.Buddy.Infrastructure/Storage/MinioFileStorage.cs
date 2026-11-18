using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using ITMO.Buddy.Application.Abstractions.Storage;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace ITMO.Buddy.Infrastructure.Storage;

internal sealed partial class MinioFileStorage : IFileStorage, IDisposable
{
    private readonly IMinioClient _storageClient;
    private readonly IMinioClient _signingClient;
    private readonly Uri _externalEndpoint;
    private readonly string _bucketName;
    private readonly int _expirySeconds;
    private int _disposed;

    public MinioFileStorage(IOptions<MinioOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var settings = options.Value;
        var (internalEndpoint, externalEndpoint) = settings.ValidateAndReadEndpoints();
        _externalEndpoint = externalEndpoint;
        _bucketName = settings.BucketName;
        _expirySeconds = (int)settings.PresignedUrlLifetime.TotalSeconds;
        _storageClient = CreateClient(internalEndpoint, settings);
        try
        {
            _signingClient = CreateClient(_externalEndpoint, settings);
        }
        catch
        {
            _storageClient.Dispose();
            throw;
        }
    }

    public async Task<StoredFile> UploadAsync(FileUpload upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ValidateObjectKey(upload.ObjectKey);
        cancellationToken.ThrowIfCancellationRequested();
        if (upload.Content is not { CanRead: true } || upload.Length <= 0 ||
            (upload.Content.CanSeek && upload.Content.Length - upload.Content.Position != upload.Length))
        {
            throw new ArgumentException("Photo upload requires a readable stream matching its declared length.", nameof(upload));
        }

        if (upload.ContentType is not ("image/jpeg" or "image/png"))
        {
            throw new ArgumentException("Photo upload requires a JPEG or PNG content type.", nameof(upload));
        }

        using var content = new PhotoUploadStream(upload.Content, upload.Length, cancellationToken);
        var args = new PutObjectArgs().WithBucket(_bucketName).WithObject(upload.ObjectKey)
            .WithStreamData(content).WithObjectSize(upload.Length).WithContentType(upload.ContentType);
        await RunAsync(() => _storageClient.PutObjectAsync(args, cancellationToken), "upload", cancellationToken);
        return new StoredFile(upload.ObjectKey);
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        ValidateObjectKey(objectKey);
        var args = new RemoveObjectArgs().WithBucket(_bucketName).WithObject(objectKey);
        try
        {
            await RunAsync(() => _storageClient.RemoveObjectAsync(args, cancellationToken), "delete", cancellationToken);
        }
        catch (MinioStorageException error) when (error.Failure == MinioFailure.ObjectNotFound)
        {
            // The SDK can infer NoSuchKey for a bodyless object-path 404. Verify that the bucket exists.
            await CheckBucketAsync(cancellationToken);
        }
    }

    public async Task<string> GetReadUrlAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        ValidateObjectKey(objectKey);
        var args = new PresignedGetObjectArgs().WithBucket(_bucketName).WithObject(objectKey).WithExpiry(_expirySeconds);
        var url = await RunAsync(() => _signingClient.PresignedGetObjectAsync(args), "URL signing", cancellationToken);
        // With static credentials and an explicit region, the SDK signs locally and takes no cancellation token.
        cancellationToken.ThrowIfCancellationRequested();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var signedUri) ||
            signedUri.Scheme != Uri.UriSchemeHttps || signedUri.IdnHost != _externalEndpoint.IdnHost ||
            signedUri.Port != _externalEndpoint.Port)
        {
            throw new MinioStorageException("URL signing", MinioFailure.OperationFailed);
        }

        return url;
    }

    public async Task CheckBucketAsync(CancellationToken cancellationToken = default)
    {
        var args = new BucketExistsArgs().WithBucket(_bucketName);
        var exists = await RunAsync(() => _storageClient.BucketExistsAsync(args, cancellationToken),
            "bucket check", cancellationToken);
        if (!exists)
        {
            throw new MinioStorageException("bucket check", MinioFailure.BucketNotFound);
        }
    }

    internal IAsyncEnumerable<Item> ListProfileObjectsAsync(CancellationToken cancellationToken) =>
        _storageClient.ListObjectsEnumAsync(
            new ListObjectsArgs().WithBucket(_bucketName).WithPrefix("profiles/").WithRecursive(true),
            cancellationToken);

    public void Dispose()
    {
        // The concrete registration and the interface alias can both track this singleton.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _signingClient.Dispose();
        _storageClient.Dispose();
    }

    private static IMinioClient CreateClient(Uri endpoint, MinioOptions settings)
    {
        var httpClient = new HttpClient(new StorageHttpHandler());
        try
        {
            return new MinioClient().WithEndpoint(endpoint)
                .WithSSL(endpoint.Scheme == Uri.UriSchemeHttps)
                .WithRegion(settings.Region)
                .WithCredentials(settings.AccessKey, settings.SecretKey)
                .WithTimeout(30_000)
                .WithHttpClient(httpClient, disposeHttpClient: true)
                .Build();
        }
        catch
        {
            httpClient.Dispose();
            throw;
        }
    }

    private static void ValidateObjectKey(string objectKey)
    {
        if (objectKey is null || !PhotoKeyFormat().IsMatch(objectKey))
        {
            throw new ArgumentException("Object key must identify a photo under profiles/.", nameof(objectKey));
        }
    }

    // The SDK error message and response can contain private endpoints or signed request data.
    private static async Task<T> RunAsync<T>(Func<Task<T>> send, string operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await send();
        }
        catch (OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new MinioStorageException(operation, MinioFailure.Unavailable);
        }
        catch (AuthorizationException)
        {
            throw new MinioStorageException(operation, MinioFailure.AccessDenied);
        }
        catch (MinioException error)
        {
            var failure = error switch
            {
                ObjectNotFoundException => MinioFailure.ObjectNotFound,
                BucketNotFoundException => MinioFailure.BucketNotFound,
                AccessDeniedException => MinioFailure.AccessDenied,
                ConnectionException => MinioFailure.Unavailable,
                _ => MinioFailure.OperationFailed
            };
            throw new MinioStorageException(operation, failure);
        }
        catch (HttpRequestException error)
        {
            var status = error.StatusCode;
            var failure = status switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => MinioFailure.AccessDenied,
                null or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests => MinioFailure.Unavailable,
                _ when (int)status >= 500 => MinioFailure.Unavailable,
                _ => MinioFailure.OperationFailed
            };
            throw new MinioStorageException(operation, failure);
        }
        catch (Exception error) when (error is IOException or XmlException or
            InvalidOperationException { InnerException: XmlException })
        {
            throw new MinioStorageException(operation, MinioFailure.OperationFailed);
        }
    }

    private static Task RunAsync(Func<Task> send, string operation, CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            await send();
            return true;
        }, operation, cancellationToken);

    [GeneratedRegex(@"\Aprofiles/[A-Za-z0-9_-][A-Za-z0-9_.-]{0,502}\z")]
    private static partial Regex PhotoKeyFormat();

    private sealed class StorageHttpHandler() : DelegatingHandler(new SocketsHttpHandler { AllowAutoRedirect = false })
    {
        private const int MaximumListingPageBytes = 8 * 1024 * 1024;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            var status = response.StatusCode;
            var expected = request.Method == HttpMethod.Delete ? HttpStatusCode.NoContent : HttpStatusCode.OK;
            // Keep XML S3 errors available to the SDK, but reject proxy redirects and unexpected success statuses.
            if (status == expected || status == HttpStatusCode.NotFound ||
                ((int)status is >= 400 and < 500 &&
                 status is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or
                     HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests) &&
                 response.Content.Headers.ContentLength != 0))
            {
                if (request.Method == HttpMethod.Get)
                {
                    try
                    {
                        // The SDK parses each listing page synchronously. Read its body under the HTTP deadline.
                        await response.Content.LoadIntoBufferAsync(MaximumListingPageBytes, cancellationToken);
                    }
                    catch
                    {
                        response.Dispose();
                        throw;
                    }
                }

                return response;
            }

            response.Dispose();
            throw new HttpRequestException("Unexpected MinIO HTTP response.", inner: null, status);
        }
    }
}
