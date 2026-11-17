namespace ITMO.Buddy.Application.Abstractions.Storage;

/// <summary>
/// Абстракция внешнего файлового хранилища.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Сохраняет файл в хранилище.
    /// Не закрывает переданный поток.
    /// </summary>
    Task<StoredFile> UploadAsync(
        FileUpload upload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет объект по внутреннему ключу.
    /// Отсутствующий объект допускается считать
    /// уже удалённым.
    /// </summary>
    Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает ссылку для чтения файла клиентом.
    /// Ссылка может быть временной.
    /// </summary>
    Task<string> GetReadUrlAsync(
        string objectKey,
        CancellationToken cancellationToken = default);
}