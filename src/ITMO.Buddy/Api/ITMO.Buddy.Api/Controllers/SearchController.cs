using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Profiles;
using ITMO.Buddy.Application.Contracts.Search;
using Microsoft.AspNetCore.Mvc;

namespace ITMO.Buddy.Api.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController(IUserSearchService search) : ControllerBase
{
    [HttpGet("users")]
    public Task<PagedResult<ProfileCardResponse>> SearchUsers(
        [FromQuery] SearchUsersRequest request, CancellationToken cancellationToken) =>
        search.SearchAsync(request, cancellationToken);
}
