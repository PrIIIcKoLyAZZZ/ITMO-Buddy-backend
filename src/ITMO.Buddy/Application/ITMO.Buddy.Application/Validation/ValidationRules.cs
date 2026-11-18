using System.Linq.Expressions;
using System.Net.Mail;
using FluentValidation;
using ITMO.Buddy.Application.Common;
using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Application.Validation;

internal static class ValidationRules
{
    public const int MaxFilterItems = 100;
    public const int MaxSearchLength = 200;
    private static readonly int GoalCategoryCount = Enum.GetValues<GoalCategory>().Length;

    public static void AddPagination<T>(
        this AbstractValidator<T> validator,
        Expression<Func<T, int>> page,
        Expression<Func<T, int>> pageSize)
    {
        validator.RuleFor(page).InclusiveBetween(1, Pagination.MaxPage);
        validator.RuleFor(pageSize).Equal(Pagination.PageSize);
    }

    public static void TrimmedLength<T>(
        this IRuleBuilder<T, string?> rule,
        int minimum,
        int maximum)
    {
        rule.Must(value =>
            {
                var length = value?.Trim().Length ?? 0;
                return length >= minimum && length <= maximum &&
                       value?.Contains('\0') != true;
            })
            .WithMessage($"{{PropertyName}} must contain {minimum} to {maximum} characters after trimming, without null characters.");
    }

    public static void SearchText<T>(this IRuleBuilder<T, string?> rule)
    {
        rule.Must(value => string.IsNullOrWhiteSpace(value) ||
                           !value.Contains('\0') && value.Trim().Length is >= 2 and <= MaxSearchLength)
            .WithMessage($"{{PropertyName}} must contain 2 to {MaxSearchLength} characters after trimming, without null characters.");
    }

    public static bool IsEmail(string? value)
    {
        var email = value?.Trim();
        return email is { Length: > 0 and <= 254 } &&
               !email.Any(char.IsWhiteSpace) &&
               !email.Contains('\0') &&
               MailAddress.TryCreate(email, out var address) &&
               string.Equals(email, address.Address, StringComparison.OrdinalIgnoreCase) &&
               address.Host.Contains('.');
    }

    public static void GuidList<T>(
        this IRuleBuilderInitial<T, IReadOnlyList<Guid>?> rule,
        int maximum)
    {
        rule.Cascade(CascadeMode.Stop)
            .Must(values => values is null || values.Count <= maximum)
            .WithMessage($"{{PropertyName}} must contain at most {maximum} items.")
            .Must(values => values is null || values.All(id => id != Guid.Empty))
            .WithMessage("{PropertyName} must not contain an empty identifier.")
            .Must(values => values is null || values.Distinct().Count() == values.Count)
            .WithMessage("{PropertyName} must not contain duplicates.");
    }

    public static void GoalList<T>(this IRuleBuilderInitial<T, IReadOnlyList<string>?> rule)
    {
        rule.Cascade(CascadeMode.Stop)
            .Must(values => values is null || values.Count <= GoalCategoryCount)
            .WithMessage("{PropertyName} contains too many goals.")
            .Must(values => values is null || values.All(ContractEnum<GoalCategory>.IsValid))
            .WithMessage("{PropertyName} contains an unknown goal.")
            .Must(values => values is null ||
                            values.Select(ContractEnum<GoalCategory>.Parse).Distinct().Count() == values.Count)
            .WithMessage("{PropertyName} must not contain duplicate goals.");
    }
}
