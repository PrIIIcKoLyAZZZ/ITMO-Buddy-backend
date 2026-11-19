namespace ITMO.Buddy.Application.Contracts.Conversations;

/// <summary>
/// Запрос отправки сообщения в существующий диалог.
/// </summary>
public sealed record SendMessageRequest
{
    /// <summary>
    /// Текст сообщения от 1 до 2000 символов
    /// после удаления пробелов по краям.
    /// </summary>
    public required string Text { get; init; }
}