using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Application.Abstractions.Realtime;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Abstractions.Storage;
using ITMO.Buddy.Application.Abstractions.Time;
using ITMO.Buddy.Application.Common;
using ITMO.Buddy.Application.Contracts.Conversations;
using ITMO.Buddy.Application.Contracts.Likes;
using ITMO.Buddy.Application.Errors;
using ITMO.Buddy.Domain.Conversations;
using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Domain.Interactions;
using ITMO.Buddy.Domain.Notifications;

namespace ITMO.Buddy.Application.Interactions;

internal sealed class LikeService(
    CurrentUserGuard currentUser,
    IUserPairLock pairLock,
    IUserLikeRepository likes,
    IUserMatchRepository matches,
    IConversationRepository conversations,
    INotificationRepository notifications,
    IUserSearchQueries profiles,
    ITransactionRunner transactions,
    IDateTimeProvider clock,
    IFileStorage files,
    IRealtimeNotifier notifier,
    RealtimeDelivery realtime) : ILikeService
{
    public Task<LikeResponse> LikeAsync(Guid targetUserId, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var userId = GetActorId(targetUserId);
            LikeResult saved;

            try
            {
                saved = await SaveLikeAsync(userId, targetUserId, cancellationToken);
            }
            catch (PersistenceException error) when (
                error.Code == PersistenceErrorCode.UniqueConstraintViolation &&
                error.ConstraintName is "ux_user_likes_sender_target" or "ux_user_matches_unordered_pair")
            {
                // A writer outside the pair lock won a unique-index race. Reread once after rollback.
                saved = await SaveLikeAsync(userId, targetUserId, cancellationToken);
            }

            if (saved.Match is not { } match)
            {
                return new LikeResponse(targetUserId, IsLiked: true, IsMatch: false);
            }

            var result = await MapMatchAsync(match, targetUserId, saved.TargetCard, saved.ConversationId, cancellationToken);
            if (saved.MatchCreated)
            {
                await realtime.TrySendAsync(RealtimeEventNames.MatchCreated,
                    token => notifier.NotifyMatchCreatedAsync(userId, result, token), cancellationToken);

                await realtime.TrySendAsync(RealtimeEventNames.MatchCreated, async token =>
                {
                    var targetResult = await MapMatchAsync(match, userId, saved.ActorCard, saved.ConversationId, token);
                    await notifier.NotifyMatchCreatedAsync(targetUserId, targetResult, token);
                }, cancellationToken);
            }

            return new LikeResponse(targetUserId, IsLiked: true, IsMatch: true) { Match = result };
        });

    public Task<LikeResponse> UnlikeAsync(Guid targetUserId, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var userId = GetActorId(targetUserId);
            var saved = await transactions.ExecuteAsync(async token =>
            {
                await pairLock.AcquireAsync(userId, targetUserId, token);
                await currentUser.RequireActiveUserAsync(token);

                // Withdrawing an outgoing like remains possible after a block or a visibility change.
                var like = await likes.GetDirectedAsync(userId, targetUserId, token);
                if (like is not null)
                {
                    likes.Remove(like);
                }

                var match = await matches.GetBetweenAsync(userId, targetUserId, token);
                var card = match is null ? null : await GetCardAsync(userId, targetUserId, token);
                var conversation = card is null ? null : await conversations.GetBetweenAsync(userId, targetUserId, token);
                return (Match: match, Card: card, ConversationId: conversation?.Id);
            }, cancellationToken);

            var result = saved is { Match: { } match, Card: { } card }
                ? await MapMatchAsync(match, targetUserId, card, saved.ConversationId, cancellationToken)
                : null;
            return new LikeResponse(targetUserId, IsLiked: false, IsMatch: saved.Match is not null) { Match = result };
        });

    private Task<LikeResult> SaveLikeAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken) =>
        transactions.ExecuteAsync(async token =>
        {
            await pairLock.AcquireAsync(userId, targetUserId, token);
            await currentUser.RequireActiveUserAsync(token);

            var targetCard = await GetCardAsync(userId, targetUserId, token)
                ?? throw AppException.NotFound("user_like.target_unavailable", "The target user is unavailable.");

            var now = clock.UtcNow;
            if (!await likes.ExistsAsync(userId, targetUserId, token))
            {
                likes.Add(new UserLike(Guid.NewGuid(), userId, targetUserId, now));
            }

            var match = await matches.GetBetweenAsync(userId, targetUserId, token);
            var matchCreated = false;
            if (match is null && await likes.ExistsAsync(targetUserId, userId, token))
            {
                match = new UserMatch(Guid.NewGuid(), userId, targetUserId, now);
                matches.Add(match);
                notifications.Add(new Notification(Guid.NewGuid(), userId, NotificationType.MatchCreated, match.Id, now));
                notifications.Add(new Notification(Guid.NewGuid(), targetUserId, NotificationType.MatchCreated, match.Id, now));
                matchCreated = true;
            }

            if (match is null)
            {
                return new LikeResult(null, null, false, targetCard, null);
            }

            var conversation = await conversations.GetBetweenAsync(userId, targetUserId, token);
            if (conversation is null)
            {
                await conversations.EnsureBetweenAsync(
                    new Conversation(Guid.NewGuid(), userId, targetUserId, ConversationSourceType.Match, match.Id, now),
                    token);
                conversation = await conversations.GetBetweenAsync(userId, targetUserId, token)
                    ?? throw new InvalidOperationException("The ensured conversation was not found.");
            }

            var actorCard = matchCreated ? await GetCardAsync(targetUserId, userId, token) : null;
            return new LikeResult(match, conversation.Id, matchCreated, targetCard, actorCard);
        }, cancellationToken);

    private async Task<UserSearchItem?> GetCardAsync(Guid viewerId, Guid userId, CancellationToken cancellationToken)
    {
        var cards = await profiles.GetCardsAsync(viewerId, [userId], cancellationToken);
        return cards.SingleOrDefault();
    }

    private async Task<MatchResponse> MapMatchAsync(
        UserMatch match, Guid otherUserId, UserSearchItem? card, Guid? conversationId, CancellationToken cancellationToken)
    {
        string? photoUrl = null;
        if (card?.PhotoObjectKey is { } key && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                photoUrl = await files.GetReadUrlAsync(key, cancellationToken);
            }
            catch (Exception)
            {
                // The match is committed and the photo is optional.
            }
        }

        return new MatchResponse(match.Id,
            new ConversationParticipantResponse(otherUserId, card?.FirstName, card?.LastName, photoUrl),
            match.CreatedAtUtc, conversationId);
    }

    private Guid GetActorId(Guid targetUserId)
    {
        var userId = currentUser.RequireUserId();
        if (targetUserId == Guid.Empty)
        {
            throw AppException.Validation("user_like.target_required", "A target user ID is required.");
        }

        if (targetUserId == userId)
        {
            throw AppException.Validation("user_like.self_like", "You cannot like yourself.");
        }

        return userId;
    }

    private sealed record LikeResult(
        UserMatch? Match, Guid? ConversationId, bool MatchCreated, UserSearchItem TargetCard, UserSearchItem? ActorCard);
}
