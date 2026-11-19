using ITMO.Buddy.Application.Abstractions.Realtime;
using ITMO.Buddy.Application.Contracts.Conversations;
using ITMO.Buddy.Application.Contracts.Likes;
using ITMO.Buddy.Application.Contracts.Responses;
using Microsoft.AspNetCore.SignalR;

namespace ITMO.Buddy.Api.Realtime;

internal sealed class SignalRRealtimeNotifier(IHubContext<ChatHub> hub) : IRealtimeNotifier
{
    public Task NotifyMessageReceivedAsync(
        Guid recipientUserId, MessageResponse message, CancellationToken cancellationToken = default) =>
        SendAsync(recipientUserId, RealtimeEventNames.MessageReceived, message, cancellationToken);

    public Task NotifyMatchCreatedAsync(
        Guid recipientUserId, MatchResponse match, CancellationToken cancellationToken = default) =>
        SendAsync(recipientUserId, RealtimeEventNames.MatchCreated, match, cancellationToken);

    public Task NotifyListingResponseCreatedAsync(
        Guid recipientUserId, ListingResponseDto response, CancellationToken cancellationToken = default) =>
        SendAsync(recipientUserId, RealtimeEventNames.ListingResponseCreated, response, cancellationToken);

    public Task NotifyListingResponseReviewedAsync(
        Guid recipientUserId, ListingResponseDto response, CancellationToken cancellationToken = default) =>
        SendAsync(recipientUserId, RealtimeEventNames.ListingResponseReviewed, response, cancellationToken);

    public Task NotifyUnreadCountChangedAsync(
        Guid recipientUserId, int unreadCount, CancellationToken cancellationToken = default) =>
        SendAsync(recipientUserId, RealtimeEventNames.NotificationUnreadCountChanged, unreadCount, cancellationToken);

    private Task SendAsync(Guid recipientUserId, string eventName, object payload, CancellationToken cancellationToken) =>
        hub.Clients.User(recipientUserId.ToString("D")).SendAsync(eventName, payload, cancellationToken);
}
