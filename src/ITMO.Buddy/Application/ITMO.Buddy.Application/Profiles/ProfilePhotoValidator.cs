using FluentValidation;
using ITMO.Buddy.Application.Abstractions.Profiles;

namespace ITMO.Buddy.Application.Profiles;

internal sealed class ProfilePhotoValidator : AbstractValidator<ProfilePhotoUpload>
{
    internal const int MaxBytes = 5 * 1024 * 1024;

    public ProfilePhotoValidator()
    {
        RuleFor(x => x.Content)
            .Must(content => content is { CanRead: true })
            .WithMessage("A readable photo stream is required.");
        RuleFor(x => x.Length)
            .InclusiveBetween(1L, MaxBytes)
            .WithMessage("Photo size must be between 1 byte and 5 MB.");
    }
}
