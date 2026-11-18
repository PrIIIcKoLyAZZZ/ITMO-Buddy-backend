namespace ITMO.Buddy.Infrastructure.Storage;

public sealed class MinioOptions
{
    // Use absolute origins. The scheme controls TLS independently for each client.
    public string InternalEndpoint { get; set; } = string.Empty;
    public string ExternalEndpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public TimeSpan PresignedUrlLifetime { get; set; } = TimeSpan.FromMinutes(5);
}
