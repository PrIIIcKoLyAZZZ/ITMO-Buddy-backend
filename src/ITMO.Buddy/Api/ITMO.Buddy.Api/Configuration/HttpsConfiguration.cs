using Microsoft.AspNetCore.HostFiltering;

namespace ITMO.Buddy.Api.Configuration;

internal static class HttpsConfiguration
{
    public static void AddApiHttps(this WebApplicationBuilder builder)
    {
        var allowedHosts = builder.Configuration["AllowedHosts"]?
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (allowedHosts.Length == 0 || allowedHosts.Any(host =>
                Uri.CheckHostName(host) == UriHostNameType.Unknown || host.Contains('*')))
        {
            throw new InvalidOperationException("AllowedHosts must list explicit host names or IP addresses without ports.");
        }

        builder.Services.PostConfigure<HostFilteringOptions>(options =>
        {
            options.AllowedHosts = allowedHosts;
            options.AllowEmptyHosts = false;
            options.IncludeFailureMessage = false;
        });

        var port = builder.Configuration.GetValue("Https:Port", 443);
        if (port is < 1 or > 65535)
        {
            throw new InvalidOperationException("Https:Port must be between 1 and 65535.");
        }

        builder.Services.AddHttpsRedirection(options =>
        {
            options.HttpsPort = port;
            options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
        });
        builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(30));
    }
}
