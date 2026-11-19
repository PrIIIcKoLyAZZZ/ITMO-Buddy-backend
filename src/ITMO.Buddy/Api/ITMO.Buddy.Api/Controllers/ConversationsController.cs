using ITMO.Buddy.Api.Configuration;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Conversations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ITMO.Buddy.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ConversationsController(IConversationService conversations) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ConversationResponse>> GetMine(
        [FromQuery] PaginationRequest pagination, CancellationToken cancellationToken) =>
        conversations.GetMyConversationsAsync(pagination, cancellationToken);

    [HttpGet("{id:guid}/messages")]
    public Task<PagedResult<MessageResponse>> GetMessages(
        Guid id, [FromQuery] PaginationRequest pagination, CancellationToken cancellationToken) =>
        conversations.GetMessagesAsync(id, pagination, cancellationToken);

    [HttpPost("{id:guid}/messages")]
    [EnableRateLimiting(RateLimitingConfiguration.Messages)]
    public Task<MessageResponse> SendMessage(Guid id, SendMessageRequest request, CancellationToken cancellationToken) =>
        conversations.SendMessageAsync(id, request, cancellationToken);
}
