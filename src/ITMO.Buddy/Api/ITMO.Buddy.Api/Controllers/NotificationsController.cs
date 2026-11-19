using FluentValidation;
using ITMO.Buddy.Api.Errors;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace ITMO.Buddy.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotificationsController(
    INotificationService notifications,
    IValidator<MarkNotificationsReadRequest> markReadValidator) : ControllerBase
{
    [HttpGet]
    public Task<NotificationsPageResponse> GetMine(
        [FromQuery] PaginationRequest pagination, CancellationToken cancellationToken) =>
        notifications.GetMyNotificationsAsync(pagination, cancellationToken);

    [HttpPost("read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(MarkNotificationsReadRequest request, CancellationToken cancellationToken)
    {
        var validation = await markReadValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiErrorResponses.ToActionResult(new ErrorResponse(
                "request.invalid", "Request validation failed.", ErrorCategories.Validation)
            {
                ValidationErrors = validation.Errors
                    .GroupBy(error => error.PropertyName)
                    .Select(group => new ValidationError(
                        group.Key, group.Select(error => error.ErrorMessage).Distinct().ToArray()))
                    .ToArray()
            });
        }

        if (request.MarkAll)
        {
            await notifications.MarkAllAsReadAsync(cancellationToken);
        }
        else
        {
            // The validator requires a nonempty NotificationIds collection for selective reads.
            await notifications.MarkAsReadAsync(request.NotificationIds!, cancellationToken);
        }

        return NoContent();
    }
}
