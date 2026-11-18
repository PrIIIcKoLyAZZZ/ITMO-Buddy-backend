using FluentValidation;
using ITMO.Buddy.Application.Common;
using ITMO.Buddy.Application.Contracts.Profiles;
using ITMO.Buddy.Application.Contracts.Recommendations;
using ITMO.Buddy.Application.Contracts.Search;
using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Domain.Users;

namespace ITMO.Buddy.Application.Validation;

internal sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).TrimmedLength(0, 100);
        RuleFor(x => x.LastName).TrimmedLength(0, 100);
        RuleFor(x => x.FacultyId).NotEqual(Guid.Empty);
        RuleFor(x => x.Course).GreaterThan(0);
        RuleFor(x => x.AboutMe).TrimmedLength(0, UserProfile.MaxAboutMeLength);
        RuleFor(x => x.Contact).TrimmedLength(0, 200);
        RuleFor(x => x.Goals).NotNull();
        RuleFor(x => x.Goals).GoalList();
        RuleFor(x => x.InterestIds).NotNull();
        RuleFor(x => x.InterestIds).GuidList(UserProfile.MaxInterestsCount);
        RuleFor(x => x.SkillIds).NotNull();
        RuleFor(x => x.SkillIds).GuidList(UserProfile.MaxSkillsCount);
    }
}

internal sealed class SearchUsersRequestValidator : AbstractValidator<SearchUsersRequest>
{
    public SearchUsersRequestValidator()
    {
        RuleFor(x => x.SearchText).SearchText();
        RuleFor(x => x.GoalCategories).GoalList();
        RuleFor(x => x.InterestIds).GuidList(ValidationRules.MaxFilterItems);
        RuleFor(x => x.SkillIds).GuidList(ValidationRules.MaxFilterItems);
        RuleFor(x => x.FacultyIds).GuidList(ValidationRules.MaxFilterItems);
        RuleFor(x => x.Courses).Cascade(CascadeMode.Stop)
            .Must(values => values is null || values.Count <= ValidationRules.MaxFilterItems)
            .WithMessage($"Courses must contain at most {ValidationRules.MaxFilterItems} items.")
            .Must(values => values is null || values.All(course => course > 0))
            .WithMessage("Courses must contain positive numbers.")
            .Must(values => values is null || values.Distinct().Count() == values.Count)
            .WithMessage("Courses must not contain duplicates.");
        this.AddPagination(x => x.Page, x => x.PageSize);
    }
}

internal sealed class GetRecommendationsRequestValidator : AbstractValidator<GetRecommendationsRequest>
{
    public GetRecommendationsRequestValidator()
    {
        RuleFor(x => x.GoalCategory).Must(ContractEnum<GoalCategory>.IsValid)
            .When(x => x.GoalCategory is not null)
            .WithMessage("Goal category is invalid.");
        this.AddPagination(x => x.Page, x => x.PageSize);
    }
}
