using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Application.Abstractions.Storage;
using ITMO.Buddy.Application.Contracts.Dictionaries;
using ITMO.Buddy.Application.Contracts.Listings;
using ITMO.Buddy.Application.Contracts.Profiles;
using ITMO.Buddy.Application.Contracts.Responses;
using Listing = ITMO.Buddy.Domain.Listings.Listing;
using Response = ITMO.Buddy.Domain.Listings.ListingResponse;

namespace ITMO.Buddy.Application.Listings;

internal sealed class ListingPresentation(IFileStorage files)
{
    private const int PreviewLength = 200;

    public async Task<ListingAuthorResponse> MapAuthorAsync(
        Guid userId,
        ListingAuthorItem? author,
        CancellationToken cancellationToken) =>
        new(userId, author?.FirstName, author?.LastName,
            await TryGetPhotoAsync(author?.PhotoObjectKey, cancellationToken));

    public static ListingResponse MapDetails(Listing listing, ListingAuthorResponse author) =>
        new(listing.Id, author, listing.Title, listing.Description, listing.Category.ToString(),
            listing.RequiredSkills.OrderBy(skill => skill.NormalizedName).ThenBy(skill => skill.Id)
                .Select(skill => new DictionaryItemResponse(skill.Id, skill.Name)).ToArray(),
            listing.RequiredParticipantsCount, listing.AcceptedParticipantsCount, listing.Format.ToString(),
            listing.DeadlineUtc, listing.EventAtUtc, listing.Status.ToString(),
            listing.CreatedAtUtc, listing.UpdatedAtUtc);

    public static ListingCardResponse MapListingCard(ListingSearchItem listing, ListingAuthorResponse author) =>
        new(listing.Id, author, listing.Title, Preview(listing.Description),
            listing.Category.ToString(), listing.Format.ToString(), listing.RequiredParticipantsCount,
            listing.AcceptedParticipantsCount, listing.DeadlineUtc, listing.EventAtUtc,
            listing.Status.ToString(), listing.CreatedAtUtc);

    public async Task<ProfileCardResponse> MapCandidateAsync(
        Guid userId,
        UserSearchItem? candidate,
        CancellationToken cancellationToken) =>
        new(
            userId,
            candidate?.FirstName,
            candidate?.LastName,
            await TryGetPhotoAsync(candidate?.PhotoObjectKey, cancellationToken),
            candidate?.Course,
            candidate is { FacultyId: { } facultyId, FacultyName: { } facultyName }
                ? new DictionaryItemResponse(facultyId, facultyName)
                : null,
            candidate?.Goals.Select(goal => goal.ToString()).ToArray() ?? [],
            candidate?.Interests.Select(item => new DictionaryItemResponse(item.Id, item.Name)).ToArray() ?? []);

    public static ListingResponseDto MapResponse(Response response, ProfileCardResponse candidate) =>
        new(response.Id, response.ListingId, candidate, response.Message,
            response.Status.ToString(), response.CreatedAtUtc, response.DecidedAtUtc);

    private async Task<string?> TryGetPhotoAsync(string? key, CancellationToken cancellationToken)
    {
        if (key is null || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        try
        {
            return await files.GetReadUrlAsync(key, cancellationToken);
        }
        catch (Exception)
        {
            // An optional photo must not turn a committed write into an error.
            return null;
        }
    }

    private static string Preview(string description)
    {
        if (description.Length <= PreviewLength)
        {
            return description;
        }

        var end = PreviewLength - 1;
        if (char.IsHighSurrogate(description[end - 1]))
        {
            end--;
        }

        return description[..end].TrimEnd() + "…";
    }
}
