using ITMO.Buddy.Api.Errors;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Dictionaries;
using Microsoft.AspNetCore.Mvc;

namespace ITMO.Buddy.Api.Controllers;

[ApiController]
[Route("api/dictionaries")]
public sealed class DictionariesController(IDictionaryService dictionaries) : ControllerBase
{
    [HttpGet("{type}")]
    public async Task<ActionResult<IReadOnlyList<DictionaryItemResponse>>> Get(
        string type, [FromQuery] DictionarySearchRequest request, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case "faculties":
                return Ok(await dictionaries.GetFacultiesAsync(cancellationToken));
            case "interests":
                return Ok(await dictionaries.SearchInterestsAsync(request, cancellationToken));
            case "skills":
                return Ok(await dictionaries.SearchSkillsAsync(request, cancellationToken));
            default:
                return ApiErrorResponses.ToActionResult(new ErrorResponse(
                    "dictionary.type_not_found", "Dictionary type was not found.", ErrorCategories.NotFound));
        }
    }

    [HttpPost("interests")]
    public Task<DictionaryItemResponse> CreateInterest(
        CreateDictionaryItemRequest request, CancellationToken cancellationToken) =>
        dictionaries.CreateInterestAsync(request, cancellationToken);

    [HttpPost("skills")]
    public Task<DictionaryItemResponse> CreateSkill(
        CreateDictionaryItemRequest request, CancellationToken cancellationToken) =>
        dictionaries.CreateSkillAsync(request, cancellationToken);
}
