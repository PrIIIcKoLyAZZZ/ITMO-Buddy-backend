using ITMO.Buddy.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace ITMO.Buddy.Api.Controllers;

[ApiController]
[Route("api/blocks")]
public sealed class BlocksController(IBlockService blocks) : ControllerBase
{
    [HttpPost("{userId:guid}")]
    public async Task<IActionResult> Block(Guid userId, CancellationToken cancellationToken)
    {
        await blocks.BlockAsync(userId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Unblock(Guid userId, CancellationToken cancellationToken)
    {
        await blocks.UnblockAsync(userId, cancellationToken);
        return NoContent();
    }
}
