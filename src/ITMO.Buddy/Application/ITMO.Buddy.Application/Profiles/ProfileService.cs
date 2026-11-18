using FluentValidation;
using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Abstractions.Profiles;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Abstractions.Storage;
using ITMO.Buddy.Application.Common;
using ITMO.Buddy.Application.Contracts.Dictionaries;
using ITMO.Buddy.Application.Contracts.Profiles;
using ITMO.Buddy.Application.Errors;
using ITMO.Buddy.Application.Validation;
using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Domain.Users;

namespace ITMO.Buddy.Application.Profiles;

internal sealed class ProfileService(
    CurrentUserGuard currentUser,
    IUserRepository users,
    IUserProfileRepository profiles,
    IUserBlockRepository blocks,
    IReferenceDataRepository referenceData,
    IAccountMaintenanceRepository accounts,
    ITransactionRunner transactions,
    IFileStorage files,
    IFileCleanupQueue cleanup,
    IValidator<UpdateProfileRequest> updateValidator,
    IValidator<ProfilePhotoUpload> photoValidator) : IProfileService
{
    public Task<ProfileResponse> GetMyProfileAsync(CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            var profile = await GetRequiredProfileAsync(user.Id, cancellationToken);
            return await ToResponseAsync(profile, includeContact: true, cancellationToken);
        });

    public Task<ProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var viewer = await currentUser.RequireActiveUserAsync(cancellationToken);
            if (userId == Guid.Empty)
            {
                throw AppException.Validation("profile.invalid_user_id", "A valid user ID is required.");
            }

            var ownProfile = viewer.Id == userId;
            if (!ownProfile &&
                (await users.GetByIdAsync(userId, cancellationToken) is not { IsActive: true } ||
                 await blocks.ExistsInEitherDirectionAsync(viewer.Id, userId, cancellationToken)))
            {
                throw ProfileNotFound();
            }

            var profile = await GetRequiredProfileAsync(userId, cancellationToken);
            if (!ownProfile && !profile.IsVisible)
            {
                throw ProfileNotFound();
            }

            return await ToResponseAsync(profile, ownProfile, cancellationToken);
        });

    public Task<ProfileResponse> UpdateMyProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var userId = currentUser.RequireUserId();
            await updateValidator.ValidateRequestAsync(request, cancellationToken);

            return await transactions.ExecuteAsync(async token =>
            {
                await LockActiveAccountAsync(userId, token);
                var profile = await GetRequiredProfileAsync(userId, token);

                if (request.FacultyId is { } facultyId &&
                    await referenceData.GetFacultyByIdAsync(facultyId, token) is null)
                {
                    throw AppException.Validation("profile.faculty_not_found", "The selected faculty does not exist.");
                }

                var interests = await referenceData.GetInterestsByIdsAsync(request.InterestIds, token);
                if (interests.Count != request.InterestIds.Count)
                {
                    throw AppException.Validation("profile.interest_not_found", "One or more selected interests do not exist.");
                }

                var skills = await referenceData.GetSkillsByIdsAsync(request.SkillIds, token);
                if (skills.Count != request.SkillIds.Count)
                {
                    throw AppException.Validation("profile.skill_not_found", "One or more selected skills do not exist.");
                }

                profile.UpdateName(request.FirstName, request.LastName);
                profile.SetFaculty(request.FacultyId);
                profile.SetCourse(request.Course);
                profile.UpdateAboutMe(request.AboutMe);
                profile.UpdateContact(request.Contact);
                profile.SetVisibility(request.IsVisible);
                profile.SetGoals(request.Goals.Select(ContractEnum<GoalCategory>.Parse));
                ReplaceTags(profile, interests, skills);

                return await ToResponseAsync(profile, includeContact: true, token);
            }, cancellationToken);
        });

    public Task<UploadPhotoResponse> UploadMyPhotoAsync(
        ProfilePhotoUpload upload,
        CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            await photoValidator.ValidateRequestAsync(upload, cancellationToken);

            using var content = await ProfilePhoto.ReadAsync(upload, cancellationToken);
            var (extension, contentType) = ProfilePhoto.DetectFormat(content);
            var fileName = $"{Guid.NewGuid():N}.{extension}";
            var key = $"profiles/{fileName}";

            // Record the cleanup intent before the first file storage operation.
            await transactions.ExecuteAsync(token => cleanup.EnqueueAsync(key, token), cancellationToken);

            (UploadPhotoResponse Response, string? PreviousKey) result;
            var uploadConfirmed = false;
            try
            {
                result = await transactions.ExecuteAsync(async token =>
                {
                    await LockActiveAccountAsync(user.Id, token);
                    var profile = await GetRequiredProfileAsync(user.Id, token);
                    if (!await cleanup.TryTakeAsync(key, token))
                    {
                        throw AppException.Conflict("profile.photo.upload_expired", "The upload could not be started. Try again.");
                    }

                    var stored = await files.UploadAsync(
                        new FileUpload(content, key, fileName, contentType, content.Length), token);
                    if (!string.Equals(stored.ObjectKey, key, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("File storage returned an unexpected object key.");
                    }

                    uploadConfirmed = true;
                    var url = await files.GetReadUrlAsync(key, token);
                    var previousKey = profile.PhotoObjectKey;
                    if (previousKey is not null)
                    {
                        await cleanup.EnqueueAsync(previousKey, token);
                    }

                    profile.UpdatePhoto(key);
                    return (new UploadPhotoResponse(url), previousKey);
                }, cancellationToken);
            }
            catch (PersistenceException error) when (
                error.Code is PersistenceErrorCode.TransactionFailure or PersistenceErrorCode.TransactionOutcomeUnknown)
            {
                // Leave the durable intent for reconciliation if the database outcome is uncertain.
                throw;
            }
            catch when (uploadConfirmed)
            {
                await TryCleanPhotoAsync(key, cancellationToken);
                throw;
            }

            await TryCleanPhotoAsync(result.PreviousKey, cancellationToken);
            return result.Response;
        });

    public Task DeleteMyAccountAsync(CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var key = await transactions.ExecuteAsync(async token =>
            {
                var userId = currentUser.RequireUserId();
                await LockActiveAccountAsync(userId, token);

                var profile = await profiles.GetByUserIdAsync(userId, token);
                if (profile?.PhotoObjectKey is { } objectKey)
                {
                    await cleanup.EnqueueAsync(objectKey, token);
                }

                await accounts.DeleteAsync(userId, token);
                return profile?.PhotoObjectKey;
            }, cancellationToken);

            await TryCleanPhotoAsync(key, cancellationToken);
        });

    private async Task TryCleanPhotoAsync(string? key, CancellationToken cancellationToken)
    {
        if (key is null || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await transactions.ExecuteAsync(async token =>
            {
                if (await cleanup.TryTakeAsync(key, token))
                {
                    await files.DeleteAsync(key, token);
                }
            }, cancellationToken);
        }
        catch (Exception)
        {
            // A failed deletion rolls back the claim. The durable entry can be processed again.
        }
    }

    private async Task LockActiveAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await accounts.LockAsync(userId, cancellationToken))
        {
            throw AppException.Unauthorized("auth.required", "Authentication is required.");
        }

        await currentUser.RequireActiveUserAsync(cancellationToken);
    }

    private async Task<UserProfile> GetRequiredProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        await profiles.GetByUserIdAsync(userId, cancellationToken) ?? throw ProfileNotFound();

    private async Task<ProfileResponse> ToResponseAsync(
        UserProfile profile,
        bool includeContact,
        CancellationToken cancellationToken)
    {
        var faculty = profile.FacultyId is { } facultyId
            ? await referenceData.GetFacultyByIdAsync(facultyId, cancellationToken)
            : null;
        var photoUrl = profile.PhotoObjectKey is { } key
            ? await files.GetReadUrlAsync(key, cancellationToken)
            : null;

        return new ProfileResponse(
            profile.Id,
            profile.UserId,
            profile.FirstName,
            profile.LastName,
            photoUrl,
            faculty is null ? null : new DictionaryItemResponse(faculty.Id, faculty.Name),
            profile.Course,
            profile.AboutMe,
            includeContact ? profile.Contact : null,
            profile.Goals.Select(goal => goal.ToString()).ToArray(),
            profile.Interests.Select(item => new DictionaryItemResponse(item.Id, item.Name)).ToArray(),
            profile.Skills.Select(item => new DictionaryItemResponse(item.Id, item.Name)).ToArray(),
            profile.IsVisible,
            profile.IsComplete);
    }

    private static void ReplaceTags(UserProfile profile, IReadOnlyList<Interest> interests, IReadOnlyList<Skill> skills)
    {
        foreach (var existing in profile.Interests.ToArray())
        {
            profile.RemoveInterest(existing.Id);
        }

        foreach (var interest in interests)
        {
            profile.AddInterest(interest);
        }

        foreach (var existing in profile.Skills.ToArray())
        {
            profile.RemoveSkill(existing.Id);
        }

        foreach (var skill in skills)
        {
            profile.AddSkill(skill);
        }
    }

    private static AppException ProfileNotFound() =>
        AppException.NotFound("profile.not_found", "The profile was not found.");
}
