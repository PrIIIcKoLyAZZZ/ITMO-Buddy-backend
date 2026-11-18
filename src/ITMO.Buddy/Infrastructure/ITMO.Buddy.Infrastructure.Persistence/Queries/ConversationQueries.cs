using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using ITMO.Buddy.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Queries;

internal sealed class ConversationQueries(BuddyDbContext context) : IConversationQueries
{
    public async Task<QueryPage<ConversationListItem>> GetPageAsync(
        Guid viewerId, int page, CancellationToken cancellationToken)
    {
        var conversations = context.Conversations.AsNoTracking()
            .Where(conversation =>
                (conversation.FirstUserId == viewerId || conversation.SecondUserId == viewerId) &&
                conversation.SourceId != Guid.Empty &&
                (conversation.SourceType == ConversationSourceType.Match ||
                 conversation.SourceType == ConversationSourceType.ListingResponse))
            .Select(conversation => new
            {
                conversation.Id,
                conversation.CreatedAtUtc,
                OtherUserId = conversation.FirstUserId == viewerId
                    ? conversation.SecondUserId : conversation.FirstUserId
            })
            .Where(item => context.Users.Any(user => user.Id == item.OtherUserId && user.Status == UserStatus.Active) &&
                !context.UserBlocks.Any(block =>
                    (block.BlockerId == viewerId && block.BlockedUserId == item.OtherUserId) ||
                    (block.BlockerId == item.OtherUserId && block.BlockedUserId == viewerId)));

        var total = await conversations.CountAsync(cancellationToken);
        var items = await conversations.Select(item => new
            {
                item.Id,
                item.OtherUserId,
                item.CreatedAtUtc,
                LastMessageAtUtc = context.Messages.Where(message => message.ConversationId == item.Id)
                    .Max(message => (DateTimeOffset?)message.SentAtUtc)
            })
            .OrderByDescending(item => item.LastMessageAtUtc.HasValue)
            .ThenByDescending(item => item.LastMessageAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip(RepositoryPaging.GetSkip(page))
            .Take(RepositoryPaging.PageSize)
            .Select(item => new ConversationListItem(
                item.Id,
                item.OtherUserId,
                context.UserProfiles.Where(profile => profile.UserId == item.OtherUserId && profile.IsVisible)
                    .Select(profile => new ConversationParticipantItem(
                        profile.FirstName, profile.LastName, profile.PhotoObjectKey))
                    .FirstOrDefault(),
                context.Messages.Where(message => message.ConversationId == item.Id)
                    .OrderByDescending(message => message.SentAtUtc)
                    .ThenByDescending(message => message.Id)
                    .FirstOrDefault(),
                context.Messages.Count(message => message.ConversationId == item.Id &&
                    message.SenderId == item.OtherUserId && message.ReadAtUtc == null)))
            .ToArrayAsync(cancellationToken);

        return new QueryPage<ConversationListItem>(items, page, RepositoryPaging.PageSize, total);
    }
}
