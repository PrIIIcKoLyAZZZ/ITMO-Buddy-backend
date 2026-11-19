namespace ITMO.Buddy.Application.Abstractions.Profiles;

/// <summary>
/// Внутренние данные загружаемой фотографии.
/// Не зависит от ASP.NET Core.
/// </summary>
/// <param name="Content">
/// Поток содержимого файла.
/// Владельцем потока остаётся вызывающий код.
/// </param>
/// <param name="FileName">
/// Исходное имя файла.
/// </param>
/// <param name="ContentType">
/// Заявленный MIME-тип файла.
/// </param>
/// <param name="Length">
/// Размер файла в байтах.
/// </param>
public sealed record ProfilePhotoUpload(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);