using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Contracts.Likes;
using Microsoft.AspNetCore.Mvc;

namespace ITMO.Buddy.Api.Controllers;

[ApiController]
[Route("api/likes")]
public sealed class LikesController(ILikeService likes) : ControllerBase
{
    [HttpPost("{userId:guid}")]
    public Task<LikeResponse> Like(Guid userId, CancellationToken cancellationToken) =>
        likes.LikeAsync(userId, cancellationToken);

    [HttpDelete("{userId:guid}")]
    public Task<LikeResponse> Unlike(Guid userId, CancellationToken cancellationToken) =>
        likes.UnlikeAsync(userId, cancellationToken);
}
