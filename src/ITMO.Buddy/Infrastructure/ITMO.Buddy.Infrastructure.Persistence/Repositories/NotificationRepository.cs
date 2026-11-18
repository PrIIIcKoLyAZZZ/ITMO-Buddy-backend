using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Enums;
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

    public Task<int> CountForRecipientAsync(
        Guid recipientId,
        CancellationToken cancellationToken = default)
    {
        return _context.Notifications
            .AsNoTracking()
            .CountAsync(
                x => x.RecipientId == recipientId,
                cancellationToken);
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

    public Task<int> CountOwnedAsync(
        Guid recipientId, IReadOnlyCollection<Guid> notificationIds, CancellationToken cancellationToken) =>
        _context.Notifications.CountAsync(
            notification => notification.RecipientId == recipientId && notificationIds.Contains(notification.Id),
            cancellationToken);

    public Task<int> MarkReadAsync(
        Guid recipientId, IReadOnlyCollection<Guid> notificationIds,
        DateTimeOffset readAtUtc, CancellationToken cancellationToken) =>
        MarkWithLinkedMessagesAsync(recipientId,
            _context.Notifications.Where(notification =>
                notification.RecipientId == recipientId && notificationIds.Contains(notification.Id)),
            readAtUtc, cancellationToken);

    public Task<int> MarkAllReadAsync(
        Guid recipientId, DateTimeOffset readAtUtc, CancellationToken cancellationToken) =>
        MarkWithLinkedMessagesAsync(recipientId,
            _context.Notifications.Where(notification => notification.RecipientId == recipientId),
            readAtUtc, cancellationToken);

    public Task<int> MarkMessageNotificationsReadAsync(
        Guid recipientId, IReadOnlyCollection<Guid> messageIds,
        DateTimeOffset readAtUtc, CancellationToken cancellationToken) =>
        MarkReadAsync(UnreadFor(recipientId).Where(notification =>
                notification.Type == NotificationType.MessageReceived && messageIds.Contains(notification.RelatedEntityId)),
            readAtUtc, cancellationToken);

    private async Task<int> MarkWithLinkedMessagesAsync(
        Guid recipientId, IQueryable<Notification> selected, DateTimeOffset readAtUtc,
        CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Read receipt synchronization requires an active transaction.");
        }

        // Update notifications before messages, in the same order as reading conversation history.
        var changed = await MarkReadAsync(selected.Where(notification => notification.ReadAtUtc == null),
            readAtUtc, cancellationToken);

        // Include already-read notifications to repair out-of-sync receipts from earlier versions.
        // The read-state filter excludes newly inserted unread notifications from the second statement.
        var readMessageNotifications = selected.Where(notification =>
            notification.Type == NotificationType.MessageReceived && notification.ReadAtUtc != null);

        await _context.Messages.Where(message =>
                message.ReadAtUtc == null &&
                message.SenderId != recipientId &&
                readMessageNotifications.Any(notification => notification.RelatedEntityId == message.Id) &&
                _context.Conversations.Any(conversation =>
                    conversation.Id == message.ConversationId &&
                    ((conversation.FirstUserId == recipientId && conversation.SecondUserId == message.SenderId) ||
                     (conversation.SecondUserId == recipientId && conversation.FirstUserId == message.SenderId))))
            .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.ReadAtUtc,
                message => message.SentAtUtc > readAtUtc ? message.SentAtUtc : readAtUtc), cancellationToken);

        return changed;
    }

    private IQueryable<Notification> UnreadFor(Guid recipientId) =>
        _context.Notifications.Where(notification =>
            notification.RecipientId == recipientId && notification.ReadAtUtc == null);

    private static Task<int> MarkReadAsync(
        IQueryable<Notification> unread, DateTimeOffset readAtUtc, CancellationToken cancellationToken)
    {
        if (readAtUtc == default || readAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Read time must be a valid UTC timestamp.", nameof(readAtUtc));
        }

        // Only unread rows are updated. Keep the domain time invariant even if application clocks differ.
        return unread.ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAtUtc,
            notification => notification.CreatedAtUtc > readAtUtc ? notification.CreatedAtUtc : readAtUtc),
            cancellationToken);
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