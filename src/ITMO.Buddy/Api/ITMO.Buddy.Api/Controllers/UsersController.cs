using ITMO.Buddy.Api.Authentication;
using ITMO.Buddy.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace ITMO.Buddy.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IProfileService profiles) : ControllerBase
{
    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMe(CancellationToken cancellationToken)
    {
        await profiles.DeleteMyAccountAsync(cancellationToken);
        // The browser omits the auth-path cookie on this request, so always expire it on success.
        RefreshCookieManager.Clear(Response);
        return NoContent();
    }
}
