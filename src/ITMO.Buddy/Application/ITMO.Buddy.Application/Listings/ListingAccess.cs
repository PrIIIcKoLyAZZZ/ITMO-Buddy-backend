using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Errors;

namespace ITMO.Buddy.Application.Listings;

internal sealed class ListingAccess(IUserRepository users, IUserBlockRepository blocks)
{
    public async Task<bool> CanInteractAsync(Guid viewerId, Guid otherUserId, CancellationToken cancellationToken) =>
        viewerId == otherUserId ||
        (await users.GetByIdAsync(otherUserId, cancellationToken) is { IsActive: true } &&
         !await blocks.ExistsInEitherDirectionAsync(viewerId, otherUserId, cancellationToken));

    public static void RequireOwner(Guid ownerId, Guid viewerId)
    {
        if (ownerId != viewerId)
        {
            throw AppException.Forbidden("listing.owner_required", "Only the listing author may perform this operation.");
        }
    }

    public static void ValidateId(Guid id, string field)
    {
        if (id == Guid.Empty)
        {
            throw AppException.Validation("listing.invalid_id", $"A valid {field} ID is required.");
        }
    }

    public static AppException NotFound() =>
        AppException.NotFound("listing.not_found", "The listing was not found.");
}
