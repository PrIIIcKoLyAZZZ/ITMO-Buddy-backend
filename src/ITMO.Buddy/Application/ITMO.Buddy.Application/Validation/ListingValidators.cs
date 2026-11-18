using System.Linq.Expressions;
using FluentValidation;
using ITMO.Buddy.Application.Abstractions.Time;
using ITMO.Buddy.Application.Common;
using ITMO.Buddy.Application.Contracts.Listings;
using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Domain.Listings;

namespace ITMO.Buddy.Application.Validation;

internal sealed class CreateListingRequestValidator : AbstractValidator<CreateListingRequest>
{
    public CreateListingRequestValidator(IDateTimeProvider clock)
    {
        this.AddListingRules(clock, x => x.Title, x => x.Description, x => x.Category,
            x => x.RequiredSkillIds, x => x.RequiredParticipantsCount, x => x.Format,
            x => x.DeadlineUtc, x => x.EventAtUtc);
    }
}

internal sealed class UpdateListingRequestValidator : AbstractValidator<UpdateListingRequest>
{
    public UpdateListingRequestValidator(IDateTimeProvider clock)
    {
        this.AddListingRules(clock, x => x.Title, x => x.Description, x => x.Category,
            x => x.RequiredSkillIds, x => x.RequiredParticipantsCount, x => x.Format,
            x => x.DeadlineUtc, x => x.EventAtUtc);
    }
}

internal sealed class SearchListingsRequestValidator : AbstractValidator<SearchListingsRequest>
{
    public SearchListingsRequestValidator()
    {
        RuleFor(x => x.SearchText).SearchText();
        RuleFor(x => x.Category).Must(ContractEnum<GoalCategory>.IsValid)
            .When(x => x.Category is not null)
            .WithMessage("Listing category is invalid.");
        RuleFor(x => x.Format).Must(ContractEnum<ListingFormat>.IsValid)
            .When(x => x.Format is not null)
            .WithMessage("Listing format is invalid.");
        RuleFor(x => x.SkillIds).GuidList(ValidationRules.MaxFilterItems);
        this.AddPagination(x => x.Page, x => x.PageSize);
    }
}

internal static class ListingValidationRules
{
    public static void AddListingRules<T>(
        this AbstractValidator<T> validator,
        IDateTimeProvider clock,
        Expression<Func<T, string?>> title,
        Expression<Func<T, string?>> description,
        Expression<Func<T, string>> category,
        Expression<Func<T, IReadOnlyList<Guid>?>> skills,
        Expression<Func<T, int>> participants,
        Expression<Func<T, string>> format,
        Expression<Func<T, DateTimeOffset?>> deadline,
        Expression<Func<T, DateTimeOffset?>> eventAt)
    {
        validator.RuleFor(title).TrimmedLength(1, Listing.MaxTitleLength);
        validator.RuleFor(description).TrimmedLength(1, Listing.MaxDescriptionLength);

        validator.RuleFor(category).Must(ContractEnum<GoalCategory>.IsValid)
            .WithMessage("Listing category is invalid.");
        validator.RuleFor(format).Must(ContractEnum<ListingFormat>.IsValid)
            .WithMessage("Listing format is invalid.");
        validator.RuleFor(skills).NotNull();
        validator.RuleFor(skills).GuidList(ValidationRules.MaxFilterItems);
        validator.RuleFor(participants)
            .InclusiveBetween(Listing.MinParticipantsCount, Listing.MaxParticipantsCount);

        var getEventAt = eventAt.Compile();
        validator.RuleFor(deadline).Must(value => IsFutureUtc(value, clock))
            .WithMessage("Deadline must be a future UTC timestamp.");
        validator.RuleFor(eventAt).Must(value => IsFutureUtc(value, clock))
            .WithMessage("Event date must be a future UTC timestamp.");
        validator.RuleFor(deadline)
            .Must((request, value) => value is null || getEventAt(request) is not { } end || value <= end)
            .WithMessage("Deadline cannot be later than the event date.");
    }

    private static bool IsFutureUtc(DateTimeOffset? value, IDateTimeProvider clock) =>
        value is null || value.Value != default && value.Value.Offset == TimeSpan.Zero && value.Value > clock.UtcNow;
}
