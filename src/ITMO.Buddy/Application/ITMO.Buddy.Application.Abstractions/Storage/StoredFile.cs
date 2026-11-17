namespace ITMO.Buddy.Application.Abstractions.Storage;

/// <summary>
/// Результат успешного сохранения файла.
/// </summary>
/// <param name="ObjectKey">
/// Стабильный внутренний идентификатор объекта.
/// </param>
public sealed record StoredFile(
    string ObjectKey);