using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Abstractions.Realtime;
using ITMO.Buddy.Application.Common;

namespace ITMO.Buddy.Application.Notifications;

internal sealed class UnreadNotificationDelivery(
    INotificationRepository notifications,
    IRealtimeNotifier notifier,
    RealtimeDelivery realtime)
{
    public Task PublishAsync(Guid recipientId, CancellationToken cancellationToken) =>
        realtime.TrySendAsync(RealtimeEventNames.NotificationUnreadCountChanged, async token =>
        {
            var count = await notifications.CountUnreadAsync(recipientId, token);
            await notifier.NotifyUnreadCountChangedAsync(recipientId, count, token);
        }, cancellationToken);
}
