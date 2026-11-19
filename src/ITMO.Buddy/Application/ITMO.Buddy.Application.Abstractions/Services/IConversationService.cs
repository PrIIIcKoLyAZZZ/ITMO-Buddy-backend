using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Conversations;

namespace ITMO.Buddy.Application.Abstractions.Services;

/// <summary>
/// Сервис диалогов и сообщений.
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// Возвращает диалоги текущего пользователя.
    /// </summary>
    Task<PagedResult<ConversationResponse>>
        GetMyConversationsAsync(
            PaginationRequest pagination,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает историю сообщений диалога.
    /// Доступно только его участникам.
    /// </summary>
    Task<PagedResult<MessageResponse>> GetMessagesAsync(
        Guid conversationId,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет сообщение в существующем диалоге.
    /// Отправитель определяется сервером.
    /// </summary>
    Task<MessageResponse> SendMessageAsync(
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default);
}