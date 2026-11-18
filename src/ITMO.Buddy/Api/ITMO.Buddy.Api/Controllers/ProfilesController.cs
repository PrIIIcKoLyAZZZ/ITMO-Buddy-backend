using ITMO.Buddy.Api.Errors;
using ITMO.Buddy.Application.Abstractions.Profiles;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Contracts.Profiles;
using Microsoft.AspNetCore.Mvc;

namespace ITMO.Buddy.Api.Controllers;

[ApiController]
[Route("api/profiles")]
public sealed class ProfilesController(IProfileService profiles) : ControllerBase
{
    private const int MaxPhotoBytes = 5 * 1024 * 1024;
    private const int MaxPhotoRequestBytes = MaxPhotoBytes + 64 * 1024;

    [HttpGet("me")]
    public Task<ProfileResponse> GetMe(CancellationToken cancellationToken) =>
        profiles.GetMyProfileAsync(cancellationToken);

    [HttpPut("me")]
    public Task<ProfileResponse> UpdateMe(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        profiles.UpdateMyProfileAsync(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<ProfileResponse> GetByUserId(Guid id, CancellationToken cancellationToken) =>
        profiles.GetProfileAsync(id, cancellationToken);

    [HttpPost("me/photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxPhotoRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxPhotoRequestBytes)]
    public async Task<ActionResult<UploadPhotoResponse>> UploadPhoto(
        IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is <= 0 or > MaxPhotoBytes)
        {
            return ApiErrorResponses.ValidationResult("profile.photo.invalid_size", "file",
                "A photo between 1 byte and 5 MiB is required.");
        }

        await using var content = file.OpenReadStream();
        var upload = new ProfilePhotoUpload(content, file.FileName, file.ContentType, file.Length);
        return await profiles.UploadMyPhotoAsync(upload, cancellationToken);
    }
}
