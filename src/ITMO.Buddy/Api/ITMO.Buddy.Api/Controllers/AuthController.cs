using ITMO.Buddy.Api.Authentication;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITMO.Buddy.Api.Controllers;

[ApiController]
[Route(RefreshCookieManager.Route)]
[AllowAnonymous]
[ServiceFilter(typeof(AuthOriginGuard))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var session = await auth.RegisterAsync(request, cancellationToken);
        RefreshCookieManager.Set(Response, session);
        return StatusCode(StatusCodes.Status201Created, session.Response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await auth.LoginAsync(request, cancellationToken);
        RefreshCookieManager.Set(Response, session);
        return session.Response;
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        var session = await auth.RefreshAsync(RefreshCookieManager.Read(Request) ?? string.Empty, cancellationToken);
        RefreshCookieManager.Set(Response, session);
        return session.Response;
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(RefreshCookieManager.Read(Request), cancellationToken);
        RefreshCookieManager.Clear(Response);
        return NoContent();
    }
}
