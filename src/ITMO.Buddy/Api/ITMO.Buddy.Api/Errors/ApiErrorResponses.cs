using ITMO.Buddy.Application.Contracts.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace ITMO.Buddy.Api.Errors;

internal static class ApiErrorResponses
{
    public static ErrorResponse InternalError { get; } =
        new("application.internal_error", "An unexpected error occurred.", ErrorCategories.InternalError);

    public static Task WriteAsync(HttpContext context, ErrorResponse error)
    {
        context.Response.StatusCode = error.Category switch
        {
            ErrorCategories.Validation => StatusCodes.Status400BadRequest,
            ErrorCategories.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorCategories.Forbidden => StatusCodes.Status403Forbidden,
            ErrorCategories.NotFound => StatusCodes.Status404NotFound,
            ErrorCategories.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return context.Response.WriteAsJsonAsync(error, context.RequestAborted);
    }

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
