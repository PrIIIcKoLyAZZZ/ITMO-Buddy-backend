using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using ITMO.Buddy.Application.Abstractions.Storage;
using Microsoft.Extensions.Options;
using Minio;
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

    public MinioFileStorage(IOptions<MinioOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var settings = options.Value;
        var internalEndpoint = ReadOrigin(settings.InternalEndpoint, nameof(MinioOptions.InternalEndpoint));
        _externalEndpoint = ReadOrigin(settings.ExternalEndpoint, nameof(MinioOptions.ExternalEndpoint));
        if (_externalEndpoint.Scheme != Uri.UriSchemeHttps ||
            _externalEndpoint.HostNameType != UriHostNameType.Dns ||
            !_externalEndpoint.IdnHost.TrimEnd('.').Contains('.') ||
            _externalEndpoint.IsLoopback)
        {
            throw new ArgumentException("MinIO external endpoint must use HTTPS and a browser-facing DNS name.",
                nameof(MinioOptions.ExternalEndpoint));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(settings.AccessKey, nameof(MinioOptions.AccessKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.SecretKey, nameof(MinioOptions.SecretKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Region, nameof(MinioOptions.Region));
        if (settings.BucketName is null || !BucketNameFormat().IsMatch(settings.BucketName) ||
            settings.BucketName.Contains("..", StringComparison.Ordinal) ||
            (settings.BucketName.Contains('.') && IPAddress.TryParse(settings.BucketName, out _)))
        {
            throw new ArgumentException("MinIO bucket name is invalid.", nameof(MinioOptions.BucketName));
        }

        var lifetime = settings.PresignedUrlLifetime;
        if (lifetime < TimeSpan.FromSeconds(1) || lifetime > TimeSpan.FromDays(7) ||
            lifetime.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentException("MinIO signed URL lifetime must be a whole number of seconds from 1 to 604800.",
                nameof(MinioOptions.PresignedUrlLifetime));
        }

        _bucketName = settings.BucketName;
        _expirySeconds = (int)lifetime.TotalSeconds;
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

    public void Dispose()
    {
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

    private static Uri ReadOrigin(string value, string optionName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps) ||
            endpoint.HostNameType is UriHostNameType.Basic or UriHostNameType.Unknown ||
            endpoint.AbsolutePath != "/" || endpoint.UserInfo.Length != 0 ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            endpoint.Port == 0 || (endpoint.Scheme == Uri.UriSchemeHttp && endpoint.Port == 443) ||
            (endpoint.Scheme == Uri.UriSchemeHttps && endpoint.Port == 80))
        {
            throw new ArgumentException("MinIO endpoint must be an HTTP or HTTPS origin without credentials or a path.",
                optionName);
        }

        return endpoint;
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

    [GeneratedRegex(@"\A[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]\z")]
    private static partial Regex BucketNameFormat();

    private sealed class StorageHttpHandler() : DelegatingHandler(new SocketsHttpHandler { AllowAutoRedirect = false })
    {
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
                return response;
            }

            response.Dispose();
            throw new HttpRequestException("Unexpected MinIO HTTP response.", inner: null, status);
        }
    }
}
