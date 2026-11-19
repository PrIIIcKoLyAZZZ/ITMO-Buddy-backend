namespace ITMO.Buddy.Application.Contracts.Common;

/// <summary>
/// Публичные категории ошибок приложения.
/// Не являются HTTP status codes.
/// </summary>
public static class ErrorCategories
{
    public const string Validation = "Validation";

    public const string NotFound = "NotFound";

    public const string Conflict = "Conflict";

    public const string Forbidden = "Forbidden";

    public const string Unauthorized = "Unauthorized";

    public const string InternalError = "InternalError";
}