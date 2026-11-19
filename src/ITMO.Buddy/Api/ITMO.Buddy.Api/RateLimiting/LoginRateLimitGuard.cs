using ITMO.Buddy.Api.Configuration;
using ITMO.Buddy.Api.Errors;
using ITMO.Buddy.Application.Errors;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ITMO.Buddy.Api.RateLimiting;

internal sealed class LoginRateLimitGuard(FailedLoginAttemptStore attempts) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        using var lease = await attempts.AcquireAsync(RateLimitingConfiguration.ClientIp(http), http.RequestAborted);
        var retryAfter = lease?.GetRetryAfter();
        if (lease is null || retryAfter.HasValue)
        {
            context.Result = ApiErrorResponses.RateLimitedResult(http, "auth.login_rate_limited", retryAfter);
            return;
        }

        var executed = await next();
        if (!http.RequestAborted.IsCancellationRequested && executed.Exception is AppException error &&
            ApplicationErrorMapper.Map(error).Code == "auth.invalid_credentials")
        {
            lease.RecordFailure();
        }
    }
}
