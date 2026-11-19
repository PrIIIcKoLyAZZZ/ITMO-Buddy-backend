using System.Text;
using FluentValidation;
using ITMO.Buddy.Application.Contracts.Auth;

namespace ITMO.Buddy.Application.Validation;

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).Must(ValidationRules.IsEmail)
            .WithMessage("A valid email address is required.");

        RuleFor(x => x.Password).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(FitsBcryptInput)
            .WithMessage("Password must be valid Unicode and at most 72 bytes in UTF-8.")
            .Must(password => password.Length >= 8 && password.Any(char.IsLetter) && password.Any(char.IsDigit))
            .WithMessage("Password must contain at least 8 characters, including a letter and a digit.");

        RuleFor(x => x.ConsentToPersonalDataProcessing).Equal(true)
            .WithMessage("Consent to personal data processing is required.");
    }

    private static bool FitsBcryptInput(string password)
    {
        try
        {
            return Utf8.GetByteCount(password) <= 72;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }
}

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).Must(ValidationRules.IsEmail);
        RuleFor(x => x.Password).NotEmpty();
    }
}
