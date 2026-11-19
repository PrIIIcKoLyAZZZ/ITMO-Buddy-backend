using ITMO.Buddy.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ITMO.Buddy.Infrastructure.Persistence.Transactions;

internal static class PostgresExceptionTranslator
{
    public static bool CanTranslate(Exception exception)
    {
        return exception is
            DbUpdateException or
            NpgsqlException or
            TimeoutException;
    }

    public static PersistenceException Translate(
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is PersistenceException persistence)
        {
            return persistence;
        }

        if (exception is DbUpdateConcurrencyException)
        {
            return new PersistenceException(
                PersistenceErrorCode.ConcurrencyConflict,
                "The stored entity was modified concurrently.",
                exception);
        }

        var postgres = FindPostgresException(exception);

        if (postgres is not null)
        {
            return TranslatePostgres(
                postgres,
                exception);
        }

        if (exception is NpgsqlException npgsql)
        {
            return new PersistenceException(
                PersistenceErrorCode.ConnectionFailure,
                "A PostgreSQL connection or protocol error occurred.",
                exception,
                isRetryable: npgsql.IsTransient);
        }

        if (exception is TimeoutException)
        {
            return new PersistenceException(
                PersistenceErrorCode.ConnectionFailure,
                "The database operation timed out.",
                exception,
                isRetryable: true);
        }

        return new PersistenceException(
            PersistenceErrorCode.StorageFailure,
            "The persistence operation failed.",
            exception);
    }

    private static PersistenceException TranslatePostgres(
        PostgresException postgres,
        Exception original)
    {
        var code = postgres.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation =>
                PersistenceErrorCode.UniqueConstraintViolation,

            PostgresErrorCodes.ForeignKeyViolation =>
                PersistenceErrorCode.ForeignKeyViolation,

            PostgresErrorCodes.CheckViolation =>
                PersistenceErrorCode.CheckConstraintViolation,

            PostgresErrorCodes.NotNullViolation =>
                PersistenceErrorCode.NotNullViolation,

            PostgresErrorCodes.SerializationFailure =>
                PersistenceErrorCode.SerializationFailure,

            PostgresErrorCodes.DeadlockDetected =>
                PersistenceErrorCode.Deadlock,

            _ when postgres.SqlState.StartsWith(
                "08", StringComparison.Ordinal) =>
                PersistenceErrorCode.ConnectionFailure,

            _ => PersistenceErrorCode.StorageFailure
        };

        var message = code switch
        {
            PersistenceErrorCode.UniqueConstraintViolation =>
                "A unique database constraint was violated.",

            PersistenceErrorCode.ForeignKeyViolation =>
                "A referenced database record is missing or in use.",

            PersistenceErrorCode.CheckConstraintViolation =>
                "A database check constraint was violated.",

            PersistenceErrorCode.NotNullViolation =>
                "A required database value is missing.",

            PersistenceErrorCode.SerializationFailure =>
                "The transaction conflicted with another transaction.",

            PersistenceErrorCode.Deadlock =>
                "A database deadlock was detected.",

            PersistenceErrorCode.ConnectionFailure =>
                "The PostgreSQL connection failed.",

            _ => "The PostgreSQL operation failed."
        };

        var retryable =
            code is PersistenceErrorCode.SerializationFailure
                or PersistenceErrorCode.Deadlock ||
            (code == PersistenceErrorCode.ConnectionFailure &&
             postgres.IsTransient);

        return new PersistenceException(
            code,
            message,
            original,
            constraintName: postgres.ConstraintName,
            isRetryable: retryable);
    }

    private static PostgresException? FindPostgresException(
        Exception exception)
    {
        Exception? current = exception;

        while (current is not null)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }

            current = current.InnerException;
        }

        return null;
    }
}