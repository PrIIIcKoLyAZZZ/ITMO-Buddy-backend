using ITMO.Buddy.Api.Configuration;
using ITMO.Buddy.Api.Errors;
using ITMO.Buddy.Application.Contracts.Common;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace ITMO.Buddy.Api.Realtime;

internal sealed class ChatHubOriginGuard(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICorsPolicyProvider corsPolicies)
    {
        var origins = context.Request.Headers.Origin;
        // Native clients can omit Origin. All connections still require JWT authentication.
        if (context.GetEndpoint()?.Metadata.GetMetadata<HubMetadata>()?.HubType != typeof(ChatHub) ||
            !context.Request.Headers.ContainsKey("Origin") ||
            (origins.Count == 1 && origins[0] is { } origin &&
             CorsConfiguration.IsHttpOrigin(origin) && await IsTrustedAsync(context, origin, corsPolicies)))
        {
            await next(context);
            return;
        }

        await ApiErrorResponses.WriteAsync(context, new ErrorResponse(
            "auth.origin_forbidden", "A trusted request origin is required.", ErrorCategories.Forbidden));
    }

    private static async Task<bool> IsTrustedAsync(HttpContext context, string origin, ICorsPolicyProvider corsPolicies)
    {
        var request = context.Request;
        if (Uri.TryCreate($"{request.Scheme}://{request.Host.ToUriComponent()}", UriKind.Absolute, out var target) &&
            string.Equals(origin, target.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var policy = await corsPolicies.GetPolicyAsync(context, policyName: null);
        return policy?.IsOriginAllowed(origin) == true;
    }
}
