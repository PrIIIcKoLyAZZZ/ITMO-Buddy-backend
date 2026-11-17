using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Conversations;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

public sealed class ConversationRepository
    : IConversationRepository
{
    private readonly BuddyDbContext _context;

    public ConversationRepository(BuddyDbContext context)
    {
        _context = context;
    }

    public Task<Conversation?> GetByIdAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return _context.Conversations
            .SingleOrDefaultAsync(
                x => x.Id == conversationId,
                cancellationToken);
    }

    public Task<Conversation?> GetBetweenAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.Conversations
            .SingleOrDefaultAsync(
                x =>
                    (x.FirstUserId == firstUserId &&
                     x.SecondUserId == secondUserId) ||
                    (x.FirstUserId == secondUserId &&
                     x.SecondUserId == firstUserId),
                cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>>
        GetForUserAsync(
            Guid userId,
            int page,
            CancellationToken cancellationToken = default)
    {
        var skip = RepositoryPaging.GetSkip(page);

        return await _context.Conversations
            .AsNoTracking()
            .Where(x =>
                x.FirstUserId == userId ||
                x.SecondUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(RepositoryPaging.PageSize)
            .ToListAsync(cancellationToken);
    }

    public void Add(Conversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        _context.Conversations.Add(conversation);
    }

    public void Remove(Conversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        _context.Conversations.Remove(conversation);
    }
}