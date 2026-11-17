namespace ITMO.Buddy.Application.Abstractions.Storage;

/// <summary>
/// Универсальная модель передачи файла в хранилище.
/// </summary>
/// <param name="Content">
/// Поток содержимого файла.
/// Владельцем потока остаётся вызывающий код.
/// </param>
/// <param name="ObjectKey">
/// Серверный идентификатор объекта в хранилище.
/// Не должен формироваться из недоверенного имени файла.
/// </param>
/// <param name="FileName">
/// Исходное имя файла.
/// Считается недоверенными метаданными.
/// </param>
/// <param name="ContentType">
/// MIME-тип содержимого.
/// </param>
/// <param name="Length">
/// Размер файла в байтах.
/// </param>
public sealed record FileUpload(
    Stream Content,
    string ObjectKey,
    string FileName,
    string ContentType,
    long Length);