using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace ITMO.Buddy.Api.Configuration;

internal static class AuthorizationConfiguration
{
    public static void AddApiAuthorization(this IServiceCollection services)
    {
        var policy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .Build();

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(policy)
            .SetFallbackPolicy(policy);
    }
}
