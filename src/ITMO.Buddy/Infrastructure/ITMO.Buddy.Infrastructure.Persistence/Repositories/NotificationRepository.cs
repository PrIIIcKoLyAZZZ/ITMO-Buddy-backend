using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Notifications;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository
    : INotificationRepository
{
    private readonly BuddyDbContext _context;

    public NotificationRepository(BuddyDbContext context)
    {
        _context = context;
    }

    public Task<Notification?> GetByIdAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        return _context.Notifications
            .SingleOrDefaultAsync(
                x => x.Id == notificationId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>>
        GetForRecipientAsync(
            Guid recipientId,
            int page,
            CancellationToken cancellationToken = default)
    {
        var skip = RepositoryPaging.GetSkip(page);

        return await _context.Notifications
            .AsNoTracking()
            .Where(x => x.RecipientId == recipientId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(RepositoryPaging.PageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountUnreadAsync(
        Guid recipientId,
        CancellationToken cancellationToken = default)
    {
        return _context.Notifications
            .AsNoTracking()
            .CountAsync(
                x => x.RecipientId == recipientId &&
                     x.ReadAtUtc == null,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>>
        GetUnreadForRecipientAsync(
            Guid recipientId,
            int take,
            CancellationToken cancellationToken = default)
    {
        RepositoryPaging.ValidateBatchSize(take);

        return await _context.Notifications
            .Where(x =>
                x.RecipientId == recipientId &&
                x.ReadAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public void Add(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        _context.Notifications.Add(notification);
    }

    public void Remove(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        _context.Notifications.Remove(notification);
    }
}