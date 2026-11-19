using ITMO.Buddy.Application.Abstractions.Profiles;
using ITMO.Buddy.Application.Contracts.Profiles;

namespace ITMO.Buddy.Application.Abstractions.Services;

/// <summary>
/// Сервис управления пользовательскими профилями.
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// Возвращает профиль текущего пользователя.
    /// </summary>
    Task<ProfileResponse> GetMyProfileAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает доступный для просмотра профиль.
    /// userId — идентификатор пользователя, не профиля.
    /// </summary>
    Task<ProfileResponse> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Полностью заменяет редактируемые поля своей анкеты.
    /// Допускает сохранение незавершённого профиля.
    /// </summary>
    Task<ProfileResponse> UpdateMyProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Загружает или заменяет фотографию профиля.
    /// </summary>
    Task<UploadPhotoResponse> UploadMyPhotoAsync(
        ProfilePhotoUpload upload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет или деактивирует аккаунт
    /// согласно политике удаления данных.
    /// </summary>
    Task DeleteMyAccountAsync(
        CancellationToken cancellationToken = default);
}