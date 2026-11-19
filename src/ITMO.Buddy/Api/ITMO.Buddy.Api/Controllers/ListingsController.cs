using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Listings;
using ITMO.Buddy.Application.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace ITMO.Buddy.Api.Controllers;

[ApiController]
[Route("api/listings")]
public sealed class ListingsController(IListingService listings, IListingResponseService responses) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ListingCardResponse>> Search(
        [FromQuery] SearchListingsRequest request, CancellationToken cancellationToken) =>
        listings.SearchAsync(request, cancellationToken);

    [HttpGet("my")]
    public Task<PagedResult<ListingCardResponse>> GetMine(
        [FromQuery] PaginationRequest pagination, CancellationToken cancellationToken) =>
        listings.GetMyListingsAsync(pagination, cancellationToken);

    [HttpPost]
    [ProducesResponseType<ListingResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ListingResponse>> Create(
        CreateListingRequest request, CancellationToken cancellationToken)
    {
        var listing = await listings.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = listing.Id }, listing);
    }

    [HttpGet("{id:guid}")]
    public Task<ListingResponse> GetById(Guid id, CancellationToken cancellationToken) =>
        listings.GetByIdAsync(id, cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<ListingResponse> Update(Guid id, UpdateListingRequest request, CancellationToken cancellationToken) =>
        listings.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await listings.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        await listings.CloseAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/responses")]
    public Task<PagedResult<ListingResponseDto>> GetResponses(
        Guid id, [FromQuery] PaginationRequest pagination, CancellationToken cancellationToken) =>
        responses.GetListingResponsesAsync(id, pagination, cancellationToken);

    [HttpPost("{id:guid}/responses")]
    public Task<ListingResponseDto> Respond(
        Guid id, CreateListingResponseRequest request, CancellationToken cancellationToken) =>
        responses.RespondAsync(id, request, cancellationToken);
}
