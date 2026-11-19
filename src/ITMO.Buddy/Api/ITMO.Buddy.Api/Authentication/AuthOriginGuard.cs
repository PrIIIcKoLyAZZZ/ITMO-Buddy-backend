using ITMO.Buddy.Api.Configuration;
using ITMO.Buddy.Api.Errors;
using ITMO.Buddy.Application.Contracts.Common;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ITMO.Buddy.Api.Authentication;

internal sealed class AuthOriginGuard(ICorsPolicyProvider corsPolicies) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var origins = request.Headers.Origin;

        // Session-changing requests require Origin, including requests from non-browser clients.
        if (origins.Count == 1 && origins[0] is { } origin &&
            CorsConfiguration.IsHttpOrigin(origin) && await IsTrustedAsync(request, origin))
        {
            await next();
            return;
        }

        context.Result = ApiErrorResponses.ToActionResult(
            new ErrorResponse("auth.origin_forbidden", "A trusted request origin is required.", ErrorCategories.Forbidden));
    }

    private async Task<bool> IsTrustedAsync(HttpRequest request, string origin)
    {
        if (Uri.TryCreate($"{request.Scheme}://{request.Host.ToUriComponent()}", UriKind.Absolute, out var target) &&
            string.Equals(origin, target.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var policy = await corsPolicies.GetPolicyAsync(request.HttpContext, policyName: null);
        return policy?.IsOriginAllowed(origin) == true;
    }
}
