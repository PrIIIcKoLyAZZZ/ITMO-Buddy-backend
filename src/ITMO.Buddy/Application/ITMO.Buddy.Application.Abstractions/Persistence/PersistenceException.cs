namespace ITMO.Buddy.Application.Abstractions.Persistence;

public enum PersistenceErrorCode
{
    UniqueConstraintViolation = 1,
    ForeignKeyViolation = 2,
    CheckConstraintViolation = 3,
    NotNullViolation = 4,
    ConcurrencyConflict = 5,
    SerializationFailure = 6,
    Deadlock = 7,
    ConnectionFailure = 8,
    StorageFailure = 9,
    TransactionFailure = 10,
    TransactionOutcomeUnknown = 11
}

public sealed class PersistenceException : Exception
{
    public PersistenceErrorCode Code { get; }

    public string? ConstraintName { get; }

    public bool IsRetryable { get; }

    public PersistenceException(
        PersistenceErrorCode code,
        string message,
        Exception innerException,
        string? constraintName = null,
        bool isRetryable = false)
        : base(message, innerException)
    {
        Code = code;
        ConstraintName = constraintName;
        IsRetryable = isRetryable;
    }
}