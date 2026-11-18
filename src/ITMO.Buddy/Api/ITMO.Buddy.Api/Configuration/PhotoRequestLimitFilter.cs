using ITMO.Buddy.Api.Errors;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ITMO.Buddy.Api.Configuration;

internal sealed class PhotoRequestLimitFilter : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var request = http.Request;
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize is { } limit &&
            request.ContentLength > limit)
        {
            context.Result = ApiErrorResponses.PayloadTooLargeResult();
            return;
        }

        if (request.HasFormContentType)
        {
            // Preserve the server's 413 before MVC turns a form-read failure into ModelState.
            // A successful parse is cached by the form feature and reused by the IFormFile binder.
            try
            {
                await request.ReadFormAsync(http.RequestAborted);
            }
            catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                context.Result = ApiErrorResponses.PayloadTooLargeResult();
                return;
            }
            catch (InvalidDataException)
            {
                context.Result = ApiErrorResponses.ToActionResult(
                    ApiErrorResponses.DescribeClientError(StatusCodes.Status400BadRequest)!);
                return;
            }
        }

        await next();
    }
}
