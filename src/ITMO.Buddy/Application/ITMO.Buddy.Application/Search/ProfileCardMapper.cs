using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Application.Abstractions.Storage;
using ITMO.Buddy.Application.Contracts.Dictionaries;
using ITMO.Buddy.Application.Contracts.Profiles;

namespace ITMO.Buddy.Application.Search;

internal sealed class ProfileCardMapper(IFileStorage files)
{
    public async Task<ProfileCardResponse> MapAsync(
        UserSearchItem item,
        CancellationToken cancellationToken)
    {
        var photoUrl = item.PhotoObjectKey is { } key
            ? await files.GetReadUrlAsync(key, cancellationToken)
            : null;

        return new ProfileCardResponse(
            item.UserId,
            item.FirstName,
            item.LastName,
            photoUrl,
            item.Course,
            item.FacultyId is { } facultyId && item.FacultyName is { } facultyName
                ? new DictionaryItemResponse(facultyId, facultyName)
                : null,
            item.Goals.Select(goal => goal.ToString()).ToArray(),
            item.Interests.Select(interest => new DictionaryItemResponse(interest.Id, interest.Name)).ToArray());
    }
}
