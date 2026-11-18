using ITMO.Buddy.Application.Abstractions.Auth;

namespace ITMO.Buddy.Api.Authentication;

internal static class RefreshCookieManager
{
    private const string Name = "itmo_buddy_refresh";
    internal const string Route = "api/auth";
    private const string Path = "/" + Route;

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Set(HttpResponse response, AuthSessionResult session)
    {
        var options = CreateOptions();
        options.Expires = session.RefreshTokenExpiresAtUtc;
        response.Cookies.Append(Name, session.RefreshToken, options);
    }

    public static void Clear(HttpResponse response) =>
        response.Cookies.Delete(Name, CreateOptions());

    private static CookieOptions CreateOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path
    };
}
