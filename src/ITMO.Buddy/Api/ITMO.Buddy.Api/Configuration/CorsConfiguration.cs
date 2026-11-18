namespace ITMO.Buddy.Api.Configuration;

internal static class CorsConfiguration
{
    public static void AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Any(origin => !IsHttpOrigin(origin)))
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must contain explicit HTTP or HTTPS origins.");
        }

        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
    }

    internal static bool IsHttpOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" &&
        uri.HostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6 &&
        string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase) &&
        uri.UserInfo.Length == 0 &&
        !origin.Contains('*');
}
