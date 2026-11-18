using System.Globalization;
using ITMO.Buddy.Application.Contracts.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ITMO.Buddy.Api.Errors;

internal static class ApiErrorResponses
{
    public static ErrorResponse InternalError { get; } =
        new("application.internal_error", "An unexpected error occurred.", ErrorCategories.InternalError);

    public static Task WriteAsync(HttpContext context, ErrorResponse error, int? statusCode = null)
    {
        context.Response.StatusCode = statusCode ?? GetStatusCode(error);
        return context.Response.WriteAsJsonAsync(error, context.RequestAborted);
    }

    public static ObjectResult ToActionResult(ErrorResponse error, int? statusCode = null) =>
        new(error) { StatusCode = statusCode ?? GetStatusCode(error) };

    public static IActionResult InvalidModelState(ActionContext context)
    {
        if (context.ModelState.Values.Any(entry => entry.Errors.Any(error => IsOversized(error.Exception))))
        {
            return PayloadTooLargeResult();
        }

        return ToActionResult(new ErrorResponse("request.invalid", "The request contains invalid or missing data.",
            ErrorCategories.Validation)
        {
            ValidationErrors = context.ModelState
                .Where(field => field.Value?.Errors.Count > 0)
                .Select(field => new ValidationError(field.Key, ["Invalid or missing value."]))
                .ToArray()
        });
    }

    public static ObjectResult PayloadTooLargeResult() =>
        ToActionResult(DescribeClientError(StatusCodes.Status413PayloadTooLarge)!, StatusCodes.Status413PayloadTooLarge);

    private static bool IsOversized(Exception? exception)
    {
        for (var error = exception; error is not null; error = error.InnerException)
        {
            if (error is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
            {
                return true;
            }
        }

        return false;
    }

    public static ObjectResult ValidationResult(string code, string field, string message, int? statusCode = null) =>
        ToActionResult(new ErrorResponse(code, message, ErrorCategories.Validation)
        {
            ValidationErrors = [new ValidationError(field, [message])]
        }, statusCode);

    public static ObjectResult RateLimitedResult(HttpContext context, string code, TimeSpan? retryAfter)
    {
        SetRetryAfter(context, retryAfter);
        return ToActionResult(RateLimitedError(code), StatusCodes.Status429TooManyRequests);
    }

    public static Task WriteRateLimitedAsync(HttpContext context, TimeSpan? retryAfter)
    {
        SetRetryAfter(context, retryAfter);
        return WriteAsync(context, RateLimitedError("request.rate_limited"), StatusCodes.Status429TooManyRequests);
    }

    private static ErrorResponse RateLimitedError(string code) =>
        new(code, "Too many requests. Try again later.", "RateLimited");

    private static void SetRetryAfter(HttpContext context, TimeSpan? retryAfter)
    {
        if (retryAfter.HasValue)
        {
            context.Response.Headers.RetryAfter =
                Math.Max(1, Math.Ceiling(retryAfter.Value.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }
    }

    public static Task WriteEmptyClientErrorAsync(StatusCodeContext context)
    {
        var statusCode = context.HttpContext.Response.StatusCode;
        var error = DescribeClientError(statusCode);
        return error is null ? Task.CompletedTask : WriteAsync(context.HttpContext, error, statusCode);
    }

    public static ErrorResponse? DescribeClientError(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest =>
            new("request.invalid", "The request could not be read.", ErrorCategories.Validation),
        StatusCodes.Status404NotFound =>
            new("request.route_not_found", "The requested route was not found.", ErrorCategories.NotFound),
        StatusCodes.Status413PayloadTooLarge =>
            new("request.too_large", "The request body exceeds the permitted size.", ErrorCategories.Validation),
        StatusCodes.Status415UnsupportedMediaType =>
            new("request.unsupported_media_type", "The request media type is not supported.", ErrorCategories.Validation),
        _ => null
    };

    private static int GetStatusCode(ErrorResponse error) => error.Category switch
    {
        ErrorCategories.Validation => StatusCodes.Status400BadRequest,
        ErrorCategories.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorCategories.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCategories.NotFound => StatusCodes.Status404NotFound,
        ErrorCategories.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };

    public static Task WriteChallengeAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        context.Response.Headers.WWWAuthenticate = context.Options.Challenge;
        return WriteAsync(context.HttpContext,
            new ErrorResponse("auth.unauthorized", "Authentication is required.", ErrorCategories.Unauthorized));
    }

    public static Task WriteForbiddenAsync(HttpContext context) =>
        WriteAsync(context,
            new ErrorResponse("auth.forbidden", "Access is forbidden.", ErrorCategories.Forbidden));
}
