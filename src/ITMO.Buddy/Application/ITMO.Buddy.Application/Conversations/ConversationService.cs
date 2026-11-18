using FluentValidation;
using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Application.Abstractions.Realtime;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Abstractions.Storage;
using ITMO.Buddy.Application.Abstractions.Time;
using ITMO.Buddy.Application.Common;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Conversations;
using ITMO.Buddy.Application.Errors;
using ITMO.Buddy.Application.Notifications;
using ITMO.Buddy.Application.Validation;
using ITMO.Buddy.Domain.Conversations;
using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Domain.Notifications;

namespace ITMO.Buddy.Application.Conversations;

internal sealed class ConversationService(
    CurrentUserGuard currentUser,
    IUserRepository users,
    IUserBlockRepository blocks,
    IUserPairLock pairLock,
    IConversationRepository conversations,
    IConversationQueries queries,
    IMessageRepository messages,
    INotificationRepository notifications,
    ITransactionRunner transactions,
    IDateTimeProvider clock,
    IFileStorage files,
    IRealtimeNotifier notifier,
    RealtimeDelivery realtime,
    UnreadNotificationDelivery unreadDelivery,
    IValidator<PaginationRequest> paginationValidator,
    IValidator<SendMessageRequest> messageValidator) : IConversationService
{
    public Task<PagedResult<ConversationResponse>> GetMyConversationsAsync(
        PaginationRequest pagination, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            await paginationValidator.ValidateRequestAsync(pagination, cancellationToken);

            var page = await queries.GetPageAsync(user.Id, pagination.Page, cancellationToken);
            var items = new List<ConversationResponse>(page.Items.Count);
            foreach (var item in page.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var photoUrl = await TryGetPhotoAsync(item.OtherProfile?.PhotoObjectKey, cancellationToken);
                items.Add(new ConversationResponse(
                    item.ConversationId,
                    new ConversationParticipantResponse(
                        item.OtherUserId, item.OtherProfile?.FirstName, item.OtherProfile?.LastName, photoUrl),
                    item.LastMessage is { } message ? ToResponse(message) : null,
                    item.LastMessage?.SentAtUtc,
                    item.UnreadCount));
            }

            return new PagedResult<ConversationResponse>(items, page.Page, page.PageSize, page.TotalCount);
        });

    public Task<PagedResult<MessageResponse>> GetMessagesAsync(
        Guid conversationId, PaginationRequest pagination, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var userId = currentUser.RequireUserId();
            ValidateId(conversationId);
            await paginationValidator.ValidateRequestAsync(pagination, cancellationToken);

            var saved = await transactions.ExecuteAsync(async token =>
            {
                var (conversation, otherUserId) = await LockAvailableConversationAsync(conversationId, userId, token);
                await pairLock.AcquireReadReceiptsAsync(userId, token);
                var history = await messages.GetHistoryForReadingAsync(conversation.Id, pagination.Page, token);
                var total = await messages.CountHistoryAsync(conversation.Id, token);
                var now = clock.UtcNow;

                var received = history.Where(message => message.SenderId == otherUserId).ToArray();
                var changed = received.Length == 0
                    ? 0
                    : await notifications.MarkMessageNotificationsReadAsync(
                        userId, received.Select(message => message.Id).ToArray(), now, token);

                foreach (var message in received)
                {
                    if (message.IsUnreadFor(userId, conversation))
                    {
                        var readAt = now < message.SentAtUtc ? message.SentAtUtc : now;
                        message.MarkAsRead(conversation, userId, readAt);
                    }
                }

                var items = history.Select(ToResponse).ToArray();
                return (Page: new PagedResult<MessageResponse>(items, pagination.Page, pagination.PageSize, total),
                    NotificationsChanged: changed > 0);
            }, cancellationToken);

            if (saved.NotificationsChanged)
            {
                await unreadDelivery.PublishAsync(userId, cancellationToken);
            }

            return saved.Page;
        });

    public Task<MessageResponse> SendMessageAsync(
        Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var userId = currentUser.RequireUserId();
            ValidateId(conversationId);
            await messageValidator.ValidateRequestAsync(request, cancellationToken);

            var saved = await transactions.ExecuteAsync(async token =>
            {
                var (conversation, otherUserId) = await LockAvailableConversationAsync(conversationId, userId, token);
                var now = clock.UtcNow;
                var message = new Message(Guid.NewGuid(), conversation, userId, request.Text, now);
                messages.Add(message);
                notifications.Add(new Notification(Guid.NewGuid(), otherUserId,
                    NotificationType.MessageReceived, message.Id, now));
                return (Message: ToResponse(message), RecipientId: otherUserId);
            }, cancellationToken);

            await realtime.TrySendAsync(RealtimeEventNames.MessageReceived,
                token => notifier.NotifyMessageReceivedAsync(saved.RecipientId, saved.Message, token), cancellationToken);
            await unreadDelivery.PublishAsync(saved.RecipientId, cancellationToken);
            return saved.Message;
        });

    private async Task<(Conversation Conversation, Guid OtherUserId)> LockAvailableConversationAsync(
        Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        var initial = await GetAuthorizedConversationAsync(conversationId, userId, cancellationToken);
        var otherUserId = initial.GetOtherParticipantId(userId);
        await pairLock.AcquireAsync(userId, otherUserId, cancellationToken);

        var conversation = await GetAuthorizedConversationAsync(conversationId, userId, cancellationToken);
        await currentUser.RequireActiveUserAsync(cancellationToken);
        if (await users.GetByIdAsync(otherUserId, cancellationToken) is not { IsActive: true } ||
            await blocks.ExistsInEitherDirectionAsync(userId, otherUserId, cancellationToken))
        {
            throw Unavailable();
        }

        return (conversation, otherUserId);
    }

    private async Task<Conversation> GetAuthorizedConversationAsync(
        Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetByIdAsync(conversationId, cancellationToken);
        // The persisted origin attests to access granted at creation, including deleted listing responses.
        if (conversation is null || !conversation.ContainsUser(userId) || conversation.SourceId == Guid.Empty ||
            conversation.SourceType is not (ConversationSourceType.Match or ConversationSourceType.ListingResponse))
        {
            throw Unavailable();
        }

        return conversation;
    }

    private async Task<string?> TryGetPhotoAsync(string? key, CancellationToken cancellationToken)
    {
        if (key is null)
        {
            return null;
        }

        try
        {
            return await files.GetReadUrlAsync(key, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static MessageResponse ToResponse(Message message) =>
        new(message.Id, message.ConversationId, message.SenderId, message.Text, message.SentAtUtc, message.ReadAtUtc);

    private static void ValidateId(Guid conversationId)
    {
        if (conversationId == Guid.Empty)
        {
            throw AppException.Validation("conversation.invalid_id", "A valid conversation ID is required.");
        }
    }

    private static AppException Unavailable() =>
        AppException.NotFound("conversation.not_found", "The conversation was not found.");
}
