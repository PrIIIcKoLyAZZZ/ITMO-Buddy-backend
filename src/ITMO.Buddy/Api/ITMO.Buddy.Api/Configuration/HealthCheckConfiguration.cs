using ITMO.Buddy.Api.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace ITMO.Buddy.Api.Configuration;

internal static class HealthCheckConfiguration
{
    public static void AddApiHealthChecks(this IServiceCollection services) =>
        services.AddHealthChecks().AddCheck<PostgreSqlHealthCheck>("postgres", timeout: TimeSpan.FromSeconds(3));

    public static void MapApiHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous()
            .ExcludeFromDescription();
        app.MapHealthChecks("/health/ready")
            .AllowAnonymous()
            .ExcludeFromDescription();
    }
}
