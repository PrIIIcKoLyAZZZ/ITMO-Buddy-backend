using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Users;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly BuddyDbContext _context;

    public UserRepository(BuddyDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _context.Users
            .FirstOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);
    }

    public Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var value = PrepareEmail(email);

        return _context.Users
            .FirstOrDefaultAsync(
                x => x.Email == value,
                cancellationToken);
    }

    public Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var value = PrepareEmail(email);

        return _context.Users
            .AnyAsync(
                x => x.Email == value,
                cancellationToken);
    }

    public void Add(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        _context.Users.Add(user);
    }

    public void Remove(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        _context.Users.Remove(user);
    }

    private static string PrepareEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return email.Trim();
    }
}