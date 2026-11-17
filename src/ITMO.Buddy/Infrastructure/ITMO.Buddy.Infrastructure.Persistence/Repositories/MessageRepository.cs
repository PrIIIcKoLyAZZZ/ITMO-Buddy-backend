using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Conversations;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

public sealed class MessageRepository : IMessageRepository
{
    private readonly BuddyDbContext _context;

    public MessageRepository(BuddyDbContext context)
    {
        _context = context;
    }

    public Task<Message?> GetByIdAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        return _context.Messages
            .SingleOrDefaultAsync(
                x => x.Id == messageId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Message>>
        GetHistoryAsync(
            Guid conversationId,
            int page,
            CancellationToken cancellationToken = default)
    {
        var skip = RepositoryPaging.GetSkip(page);

        return await _context.Messages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.SentAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(RepositoryPaging.PageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountHistoryAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return _context.Messages
            .AsNoTracking()
            .CountAsync(
                x => x.ConversationId == conversationId,
                cancellationToken);
    }

    public Task<Message?> GetLatestAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return _context.Messages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.SentAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountUnreadAsync(
        Guid conversationId,
        Guid recipientId,
        CancellationToken cancellationToken = default)
    {
        return _context.Messages
            .AsNoTracking()
            .CountAsync(
                x => x.ConversationId == conversationId &&
                     x.SenderId != recipientId &&
                     x.ReadAtUtc == null,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Message>>
        GetUnreadForRecipientAsync(
            Guid conversationId,
            Guid recipientId,
            int take,
            CancellationToken cancellationToken = default)
    {
        RepositoryPaging.ValidateBatchSize(take);

        return await _context.Messages
            .Where(x =>
                x.ConversationId == conversationId &&
                x.SenderId != recipientId &&
                x.ReadAtUtc == null)
            .OrderBy(x => x.SentAtUtc)
            .ThenBy(x => x.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public void Add(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _context.Messages.Add(message);
    }

    public void Remove(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _context.Messages.Remove(message);
    }
}