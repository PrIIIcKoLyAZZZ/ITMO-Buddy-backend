using System.Security.Claims;
using System.Threading.RateLimiting;
using ITMO.Buddy.Api.Authentication;
using ITMO.Buddy.Api.Errors;
using ITMO.Buddy.Api.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;

namespace ITMO.Buddy.Api.Configuration;

internal static class RateLimitingConfiguration
{
    public const string Login = "login";
    public const string Register = "register";
    public const string Refresh = "refresh";
    public const string Tags = "tags";
    public const string Photo = "photo";
    public const string Messages = "messages";

    public static void AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration, bool development)
    {
        var limits = new RateLimitingOptions();
        configuration.GetSection("RateLimiting").Bind(limits);
        limits.Validate(development);

        services.AddSingleton(limits);
        services.AddSingleton<FailedLoginAttemptStore>();
        services.AddHostedService(provider => provider.GetRequiredService<FailedLoginAttemptStore>());
        services.AddScoped<LoginRateLimitGuard>();
        services.AddRateLimiter(options =>
        {
            options.OnRejected = (context, _) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait)
                    ? wait : (TimeSpan?)null;
                return new ValueTask(ApiErrorResponses.WriteRateLimitedAsync(context.HttpContext, retryAfter));
            };
            AddPolicy(options, Login, limits.Login, byUser: false);
            AddPolicy(options, Register, limits.Register, byUser: false);
            AddPolicy(options, Refresh, limits.Refresh, byUser: false);
            AddPolicy(options, Tags, limits.Tags, byUser: true);
            AddPolicy(options, Photo, limits.Photo, byUser: true);
            AddPolicy(options, Messages, limits.Messages, byUser: true);
        });
    }

    private static void AddPolicy(RateLimiterOptions options, string name, RequestLimit limits, bool byUser) =>
        options.AddPolicy(name, context =>
        {
            var identity = byUser && context.Features.Get<IAuthenticateResultFeature>()
                ?.AuthenticateResult?.Succeeded == true
                ? HttpCurrentUser.ReadIdentity(context.User.Identity as ClaimsIdentity)
                : null;
            var key = identity is { } user ? $"user:{user.UserId:D}" : $"ip:{ClientIp(context)}";

            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limits.PermitLimit,
                Window = limits.Window,
                QueueLimit = 0,
                AutoReplenishment = false
            });
        });

    internal static string ClientIp(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        return (address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4() : address)?.ToString() ?? "unknown";
    }
}
