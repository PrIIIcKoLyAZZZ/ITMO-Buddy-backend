using FluentValidation;
using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Abstractions.Time;
using ITMO.Buddy.Application.Common;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Notifications;
using ITMO.Buddy.Application.Errors;
using ITMO.Buddy.Application.Validation;

namespace ITMO.Buddy.Application.Notifications;

internal sealed class NotificationService(
    CurrentUserGuard currentUser,
    INotificationRepository notifications,
    ITransactionRunner transactions,
    IDateTimeProvider clock,
    IValidator<PaginationRequest> paginationValidator,
    IValidator<MarkNotificationsReadRequest> markReadValidator,
    UnreadNotificationDelivery unreadDelivery) : INotificationService
{
    public Task<NotificationsPageResponse> GetMyNotificationsAsync(
        PaginationRequest pagination, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            await paginationValidator.ValidateRequestAsync(pagination, cancellationToken);

            var total = await notifications.CountForRecipientAsync(user.Id, cancellationToken);
            var page = await notifications.GetForRecipientAsync(user.Id, pagination.Page, cancellationToken);
            var unreadCount = await notifications.CountUnreadAsync(user.Id, cancellationToken);
            var items = page.Select(notification => new NotificationResponse(
                notification.Id, notification.Type.ToString(), notification.RelatedEntityId,
                notification.CreatedAtUtc, notification.IsRead)).ToArray();

            return new NotificationsPageResponse(
                new PagedResult<NotificationResponse>(items, pagination.Page, pagination.PageSize, total),
                unreadCount);
        });

    public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            return await notifications.CountUnreadAsync(user.Id, cancellationToken);
        });

    public Task MarkAsReadAsync(
        IReadOnlyCollection<Guid> notificationIds, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            var request = new MarkNotificationsReadRequest { NotificationIds = notificationIds?.ToArray() };
            await markReadValidator.ValidateRequestAsync(request, cancellationToken);
            var ids = request.NotificationIds!;

            var changed = await transactions.ExecuteAsync(async token =>
            {
                if (await notifications.CountOwnedAsync(user.Id, ids, token) != ids.Count)
                {
                    throw AppException.NotFound("notification.not_found", "One or more notifications were not found.");
                }

                return await notifications.MarkReadAsync(user.Id, ids, clock.UtcNow, token);
            }, cancellationToken);

            if (changed > 0)
            {
                await unreadDelivery.PublishAsync(user.Id, cancellationToken);
            }
        });

    public Task MarkAllAsReadAsync(CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            var changed = await transactions.ExecuteAsync(
                token => notifications.MarkAllReadAsync(user.Id, clock.UtcNow, token), cancellationToken);

            if (changed > 0)
            {
                await unreadDelivery.PublishAsync(user.Id, cancellationToken);
            }
        });
}
