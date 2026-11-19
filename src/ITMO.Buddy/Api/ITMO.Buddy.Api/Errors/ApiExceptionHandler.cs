using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;

namespace ITMO.Buddy.Api.Errors;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        if (exception is BadHttpRequestException invalidRequest &&
            ApiErrorResponses.DescribeClientError(invalidRequest.StatusCode) is { } clientError)
        {
            await ApiErrorResponses.WriteAsync(context, clientError, invalidRequest.StatusCode);
            return true;
        }

        var error = exception is OperationCanceledException
            ? ApiErrorResponses.InternalError
            : ApplicationErrorMapper.Map(exception);

        if (error.Category == ErrorCategories.InternalError)
        {
            logger.LogError("Request {TraceIdentifier} failed with exception type {ExceptionType}.",
                context.TraceIdentifier, exception.GetType().Name);
        }

        await ApiErrorResponses.WriteAsync(context, error);
        return true;
    }
}
