namespace ITMO.Buddy.Application.Contracts.Profiles;

/// <summary>
/// Результат успешного обновления фотографии профиля.
/// </summary>
public sealed record UploadPhotoResponse(
    string PhotoUrl);