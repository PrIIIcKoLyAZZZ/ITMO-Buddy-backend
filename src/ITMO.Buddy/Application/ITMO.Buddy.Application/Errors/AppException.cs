using ITMO.Buddy.Application.Contracts.Common;

namespace ITMO.Buddy.Application.Errors;

public sealed class AppException : Exception
{
    internal ErrorResponse Error { get; }

    internal AppException(ErrorResponse error)
        : base(error.Message)
    {
        Error = error;
    }

    public static AppException Validation(
        string code,
        string message,
        IReadOnlyList<ValidationError>? errors = null) =>
        new(new ErrorResponse(code, message, ErrorCategories.Validation)
        {
            ValidationErrors = errors ?? Array.Empty<ValidationError>()
        });

    public static AppException Unauthorized(string code, string message) =>
        new(new ErrorResponse(code, message, ErrorCategories.Unauthorized));

    public static AppException Forbidden(string code, string message) =>
        new(new ErrorResponse(code, message, ErrorCategories.Forbidden));

    public static AppException NotFound(string code, string message) =>
        new(new ErrorResponse(code, message, ErrorCategories.NotFound));

    public static AppException Conflict(string code, string message) =>
        new(new ErrorResponse(code, message, ErrorCategories.Conflict));
}
