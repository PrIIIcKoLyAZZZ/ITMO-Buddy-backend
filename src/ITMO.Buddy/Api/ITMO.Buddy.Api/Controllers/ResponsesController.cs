using FluentValidation;
using ITMO.Buddy.Api.Errors;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace ITMO.Buddy.Api.Controllers;

[ApiController]
[Route("api/responses")]
public sealed class ResponsesController(
    IListingResponseService responses,
    IValidator<ReviewListingResponseRequest> reviewValidator) : ControllerBase
{
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ListingResponseDto>> Review(
        Guid id, ReviewListingResponseRequest request, CancellationToken cancellationToken)
    {
        var validation = await reviewValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiErrorResponses.ToActionResult(new ErrorResponse(
                "request.invalid", "Request validation failed.", ErrorCategories.Validation)
            {
                ValidationErrors = validation.Errors
                    .GroupBy(error => error.PropertyName)
                    .Select(group => new ValidationError(
                        group.Key, group.Select(error => error.ErrorMessage).Distinct().ToArray()))
                    .ToArray()
            });
        }

        var decision = request.Decision.Trim();
        if (decision.Equals(ListingResponseDecisions.Accept, StringComparison.OrdinalIgnoreCase))
        {
            return await responses.AcceptAsync(id, cancellationToken);
        }

        if (decision.Equals(ListingResponseDecisions.Reject, StringComparison.OrdinalIgnoreCase))
        {
            return await responses.RejectAsync(id, cancellationToken);
        }

        throw new InvalidOperationException("The review validator accepted an unsupported decision.");
    }

    [HttpGet("my")]
    public Task<PagedResult<ListingResponseDto>> GetMine(
        [FromQuery] PaginationRequest pagination, CancellationToken cancellationToken) =>
        responses.GetMyResponsesAsync(pagination, cancellationToken);
}
