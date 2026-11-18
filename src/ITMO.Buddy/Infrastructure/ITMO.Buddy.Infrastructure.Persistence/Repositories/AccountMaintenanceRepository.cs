using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

internal sealed class AccountMaintenanceRepository(BuddyDbContext context) : IAccountMaintenanceRepository
{
    public async Task<bool> LockAsync(Guid userId, CancellationToken cancellationToken)
    {
        var locked = await context.Database.SqlQuery<Guid>(
            $"""SELECT id AS "Value" FROM users WHERE id = {userId} FOR UPDATE""")
            .ToListAsync(cancellationToken);
        return locked.Count != 0;
    }

    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Take listing locks before reading response status so a concurrent acceptance
        // either commits first or loses its optimistic concurrency check.
        await context.Database.SqlQuery<Guid>($"""
            SELECT l.id AS "Value" FROM listings AS l
            WHERE l.author_id = {userId} OR EXISTS (
                SELECT 1 FROM listing_responses AS r
                WHERE r.listing_id = l.id AND r.candidate_id = {userId}
            )
            ORDER BY l.id
            FOR UPDATE OF l
            """).ToListAsync(cancellationToken);

        // Hold the conversation locks while deleting notifications and cascading messages.
        await context.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM conversations
            WHERE first_user_id = {userId} OR second_user_id = {userId}
            ORDER BY id
            FOR UPDATE
            """).ToListAsync(cancellationToken);

        var responses = context.ListingResponses.Where(response => response.CandidateId == userId);
        var ownedListings = context.Listings.Where(listing => listing.AuthorId == userId);
        var conversations = context.Conversations.Where(conversation =>
            conversation.FirstUserId == userId || conversation.SecondUserId == userId);
        var messages = context.Messages.Where(message =>
            message.SenderId == userId || conversations.Any(conversation => conversation.Id == message.ConversationId));
        var matches = context.UserMatches.Where(match =>
            match.FirstUserId == userId || match.SecondUserId == userId);

        var affectedListings = await context.Listings
            .Where(listing => listing.AuthorId != userId && responses.Any(response =>
                response.ListingId == listing.Id && response.Status == ListingResponseStatus.Accepted))
            .ToListAsync(cancellationToken);
        foreach (var listing in affectedListings)
        {
            listing.RemoveAcceptedParticipant();
        }

        // Notifications reference entities polymorphically and have no cascading FK to them.
        var removedResponses = context.ListingResponses.Where(response =>
            response.CandidateId == userId || ownedListings.Any(listing => listing.Id == response.ListingId));
        await context.Notifications.Where(notification =>
            notification.RecipientId == userId ||
            (notification.Type == NotificationType.MatchCreated &&
                matches.Any(match => match.Id == notification.RelatedEntityId)) ||
            (notification.Type == NotificationType.MessageReceived &&
                messages.Any(message => message.Id == notification.RelatedEntityId)) ||
            ((notification.Type == NotificationType.ListingResponseCreated ||
              notification.Type == NotificationType.ListingResponseAccepted ||
              notification.Type == NotificationType.ListingResponseRejected) &&
                removedResponses.Any(response => response.Id == notification.RelatedEntityId)))
            .ExecuteDeleteAsync(cancellationToken);

        await context.Messages.Where(message => message.SenderId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await conversations.ExecuteDeleteAsync(cancellationToken);
        await context.UserBlocks.Where(block => block.BlockerId == userId || block.BlockedUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.UserLikes.Where(like => like.SenderId == userId || like.TargetId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await matches.ExecuteDeleteAsync(cancellationToken);
        await responses.ExecuteDeleteAsync(cancellationToken);
        await ownedListings.ExecuteDeleteAsync(cancellationToken);
        await context.Users.Where(user => user.Id == userId).ExecuteDeleteAsync(cancellationToken);
    }
}
