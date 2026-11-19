using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Recommendations;
using Microsoft.AspNetCore.Mvc;

namespace ITMO.Buddy.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
public sealed class RecommendationsController(IRecommendationService recommendations) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<RecommendationResponse>> Get(
        [FromQuery] GetRecommendationsRequest request, CancellationToken cancellationToken) =>
        recommendations.GetRecommendationsAsync(request, cancellationToken);

    [HttpPost("{userId:guid}/skip")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Skip(Guid userId, CancellationToken cancellationToken)
    {
        await recommendations.SkipRecommendationAsync(userId, cancellationToken);
        return NoContent();
    }
}
