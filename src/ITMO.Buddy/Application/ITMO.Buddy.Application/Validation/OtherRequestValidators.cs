using FluentValidation;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Conversations;
using ITMO.Buddy.Application.Contracts.Dictionaries;
using ITMO.Buddy.Application.Contracts.Notifications;
using ITMO.Buddy.Application.Contracts.Responses;
using ITMO.Buddy.Domain.Conversations;
using ITMO.Buddy.Domain.Listings;

namespace ITMO.Buddy.Application.Validation;

internal sealed class CreateListingResponseRequestValidator : AbstractValidator<CreateListingResponseRequest>
{
    public CreateListingResponseRequestValidator()
    {
        RuleFor(x => x.Message).TrimmedLength(0, ListingResponse.MaxMessageLength);
    }
}

internal sealed class ReviewListingResponseRequestValidator : AbstractValidator<ReviewListingResponseRequest>
{
    public ReviewListingResponseRequestValidator()
    {
        RuleFor(x => x.Decision)
            .Must(value =>
                string.Equals(value?.Trim(), ListingResponseDecisions.Accept, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value?.Trim(), ListingResponseDecisions.Reject, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Decision must be Accept or Reject.");
    }
}

internal sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Text).TrimmedLength(1, Message.MaxTextLength);
    }
}

internal sealed class CreateDictionaryItemRequestValidator : AbstractValidator<CreateDictionaryItemRequest>
{
    public CreateDictionaryItemRequestValidator()
    {
        RuleFor(x => x.Name).TrimmedLength(1, 100);
    }
}

internal sealed class DictionarySearchRequestValidator : AbstractValidator<DictionarySearchRequest>
{
    public DictionarySearchRequestValidator()
    {
        RuleFor(x => x.SearchText).TrimmedLength(0, ValidationRules.MaxSearchLength);
        RuleFor(x => x.Limit).InclusiveBetween(1, 20);
    }
}

internal sealed class MarkNotificationsReadRequestValidator : AbstractValidator<MarkNotificationsReadRequest>
{
    public MarkNotificationsReadRequestValidator()
    {
        RuleFor(x => x.NotificationIds).NotEmpty().When(x => !x.MarkAll);
        RuleFor(x => x.NotificationIds).Empty().When(x => x.MarkAll)
            .WithMessage("Individual notification IDs cannot be supplied when marking all notifications.");
        RuleFor(x => x.NotificationIds).GuidList(ValidationRules.MaxFilterItems);
    }
}

internal sealed class PaginationRequestValidator : AbstractValidator<PaginationRequest>
{
    public PaginationRequestValidator()
    {
        this.AddPagination(x => x.Page, x => x.PageSize);
    }
}
