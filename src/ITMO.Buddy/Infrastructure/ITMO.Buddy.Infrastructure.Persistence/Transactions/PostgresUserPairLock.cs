using System.Buffers.Binary;
using System.Security.Cryptography;
using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Transactions;

internal sealed class PostgresUserPairLock(BuddyDbContext context) : IUserPairLock
{
    public async Task AcquireAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A user pair lock requires an active transaction.");
        }

        var key = GetKey(firstUserId, secondUserId);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }

    private static long GetKey(Guid firstUserId, Guid secondUserId)
    {
        if (firstUserId.CompareTo(secondUserId) > 0)
        {
            (firstUserId, secondUserId) = (secondUserId, firstUserId);
        }

        ReadOnlySpan<byte> purpose = "ITMO.Buddy.UserPair"u8;
        Span<byte> input = stackalloc byte[purpose.Length + 32];
        purpose.CopyTo(input);
        firstUserId.TryWriteBytes(input[purpose.Length..]);
        secondUserId.TryWriteBytes(input[(purpose.Length + 16)..]);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return BinaryPrimitives.ReadInt64BigEndian(hash);
    }
}
