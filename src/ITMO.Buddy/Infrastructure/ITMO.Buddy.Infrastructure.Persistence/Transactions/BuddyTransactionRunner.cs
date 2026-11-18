using System.Data;
using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using ITMO.Buddy.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ITMO.Buddy.Infrastructure.Persistence.Transactions;

public sealed class BuddyTransactionRunner
    : ITransactionRunner
{
    private readonly BuddyDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ProfileGoalsStore _goalsStore;

    public BuddyTransactionRunner(
        BuddyDbContext context,
        IUnitOfWork unitOfWork,
        ProfileGoalsStore goalsStore)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _goalsStore = goalsStore;
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteAsync<bool>(
            async token =>
            {
                await operation(token);
                return true;
            },
            cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        IDbContextTransaction transaction;

        try
        {
            transaction = await _context.Database
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);
        }
        catch (Exception exception) when (
            PostgresExceptionTranslator.CanTranslate(exception))
        {
            throw PostgresExceptionTranslator.Translate(
                exception);
        }

        await using (transaction)
        {
            T result;

            try
            {
                result = await operation(cancellationToken);

                await _unitOfWork.SaveChangesAsync(
                    cancellationToken);
            }
            catch (Exception original)
            {
                try
                {
                    await transaction.RollbackAsync(
                        CancellationToken.None);
                }
                catch (Exception rollbackFailure)
                {
                    throw new PersistenceException(
                        PersistenceErrorCode.TransactionFailure,
                        "The transaction and its rollback failed.",
                        new AggregateException(
                            original,
                            rollbackFailure));
                }
                finally
                {
                    ClearTrackedState();
                }

                if (PostgresExceptionTranslator.CanTranslate(
                    original))
                {
                    throw PostgresExceptionTranslator.Translate(
                        original);
                }

                throw;
            }

            try
            {
                await transaction.CommitAsync(
                    cancellationToken);
            }
            catch (PostgresException rejected) when (rejected.SqlState is
                PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation or
                PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NotNullViolation or
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
            {
                ClearTrackedState();
                throw PostgresExceptionTranslator.Translate(rejected);
            }
            catch (Exception commitFailure)
            {
                ClearTrackedState();
                throw new PersistenceException(
                    PersistenceErrorCode.TransactionOutcomeUnknown,
                    "The transaction commit outcome is unknown.",
                    commitFailure);
            }

            return result;
        }
    }

    private void ClearTrackedState()
    {
        _context.ChangeTracker.Clear();
        _goalsStore.ClearSnapshots();
    }
}