using System.Net;
using System.Text.RegularExpressions;

namespace ITMO.Buddy.Infrastructure.Storage;

public sealed partial class MinioOptions
{
    // Use absolute origins. The scheme controls TLS independently for each client.
    public string InternalEndpoint { get; set; } = string.Empty;
    public string ExternalEndpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public TimeSpan PresignedUrlLifetime { get; set; } = TimeSpan.FromMinutes(5);

    internal (Uri Internal, Uri External) ValidateAndReadEndpoints()
    {
        var internalEndpoint = ReadOrigin(InternalEndpoint, nameof(InternalEndpoint));
        var externalEndpoint = ReadOrigin(ExternalEndpoint, nameof(ExternalEndpoint));
        if (externalEndpoint.Scheme != Uri.UriSchemeHttps ||
            externalEndpoint.HostNameType != UriHostNameType.Dns ||
            !externalEndpoint.IdnHost.TrimEnd('.').Contains('.') ||
            externalEndpoint.IsLoopback)
        {
            throw new ArgumentException("MinIO external endpoint must use HTTPS and a browser-facing DNS name.",
                nameof(ExternalEndpoint));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(AccessKey, nameof(AccessKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(SecretKey, nameof(SecretKey));
        if (Region is null || !RegionFormat().IsMatch(Region))
        {
            throw new ArgumentException("MinIO region must contain only ASCII letters, digits or hyphens.",
                nameof(Region));
        }

        if (BucketName is null || !BucketNameFormat().IsMatch(BucketName) ||
            BucketName.Contains("..", StringComparison.Ordinal) ||
            (BucketName.Contains('.') && IPAddress.TryParse(BucketName, out _)))
        {
            throw new ArgumentException("MinIO bucket name is invalid.", nameof(BucketName));
        }

        if (PresignedUrlLifetime < TimeSpan.FromSeconds(1) || PresignedUrlLifetime > TimeSpan.FromDays(7) ||
            PresignedUrlLifetime.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentException("MinIO signed URL lifetime must be a whole number of seconds from 1 to 604800.",
                nameof(PresignedUrlLifetime));
        }

        return (internalEndpoint, externalEndpoint);
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

    [GeneratedRegex(@"\A[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]\z")]
    private static partial Regex BucketNameFormat();

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9-]*\z")]
    private static partial Regex RegionFormat();
}
