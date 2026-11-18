namespace ITMO.Buddy.Infrastructure.Storage;

internal enum MinioFailure
{
    ObjectNotFound,
    BucketNotFound,
    AccessDenied,
    Unavailable,
    OperationFailed
}

internal sealed class MinioStorageException(string operation, MinioFailure failure)
    : IOException($"MinIO {operation} failed ({failure}).")
{
    public MinioFailure Failure { get; } = failure;
}
