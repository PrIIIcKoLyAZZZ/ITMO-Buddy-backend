using FluentValidation;
using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Application.Abstractions.Realtime;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Abstractions.Time;
using ITMO.Buddy.Application.Common;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Responses;
using ITMO.Buddy.Application.Errors;
using ITMO.Buddy.Application.Notifications;
using ITMO.Buddy.Application.Validation;
using ITMO.Buddy.Domain.Conversations;
using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Domain.Notifications;

namespace ITMO.Buddy.Application.Listings;

internal sealed class ListingResponseService(
    CurrentUserGuard currentUser,
    IListingRepository listings,
    IUserSearchQueries profiles,
    IConversationRepository conversations,
    INotificationRepository notifications,
    ITransactionRunner transactions,
    IUserPairLock pairLock,
    IDateTimeProvider clock,
    IRealtimeNotifier notifier,
    RealtimeDelivery realtime,
    UnreadNotificationDelivery unreadDelivery,
    ListingAccess access,
    ListingPresentation presentation,
    IValidator<CreateListingResponseRequest> responseValidator,
    IValidator<PaginationRequest> paginationValidator) : IListingResponseService
{
    public Task<ListingResponseDto> RespondAsync(
        Guid listingId, CreateListingResponseRequest request, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            ListingAccess.ValidateId(listingId, "listing");
            await responseValidator.ValidateRequestAsync(request, cancellationToken);

            var saved = await CreateResponseAsync(listingId, user.Id, request.Message, cancellationToken);
            var candidate = await presentation.MapCandidateAsync(user.Id, saved.OwnCard, cancellationToken);
            var result = ListingPresentation.MapResponse(saved.Response, candidate);

            await realtime.TrySendAsync(RealtimeEventNames.ListingResponseCreated, async token =>
            {
                var visibleCard = await presentation.MapCandidateAsync(user.Id, saved.VisibleCard, token);
                await notifier.NotifyListingResponseCreatedAsync(
                    saved.AuthorId, ListingPresentation.MapResponse(saved.Response, visibleCard), token);
            }, cancellationToken);
            await unreadDelivery.PublishAsync(saved.AuthorId, cancellationToken);
            return result;
        });

    public Task<ListingResponseDto> AcceptAsync(Guid responseId, CancellationToken cancellationToken = default) =>
        ReviewAsync(responseId, NotificationType.ListingResponseAccepted, cancellationToken);

    public Task<ListingResponseDto> RejectAsync(Guid responseId, CancellationToken cancellationToken = default) =>
        ReviewAsync(responseId, NotificationType.ListingResponseRejected, cancellationToken);

    public Task<PagedResult<ListingResponseDto>> GetListingResponsesAsync(
        Guid listingId, PaginationRequest pagination, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            ListingAccess.ValidateId(listingId, "listing");
            await paginationValidator.ValidateRequestAsync(pagination, cancellationToken);

            var authorId = await listings.GetAuthorIdAsync(listingId, cancellationToken) ?? throw ListingAccess.NotFound();
            ListingAccess.RequireOwner(authorId, user.Id);

            var totalCount = await listings.CountListingResponsesAsync(listingId, cancellationToken);
            var responses = await listings.GetListingResponsesAsync(listingId, pagination.Page, cancellationToken);
            var userIds = responses.Select(response => response.CandidateId).Distinct().ToArray();
            var cards = userIds.Length == 0
                ? new Dictionary<Guid, UserSearchItem>()
                : (await profiles.GetCardsAsync(user.Id, userIds, cancellationToken)).ToDictionary(card => card.UserId);

            var items = new List<ListingResponseDto>(responses.Count);
            foreach (var response in responses)
            {
                var candidate = await presentation.MapCandidateAsync(
                    response.CandidateId, cards.GetValueOrDefault(response.CandidateId), cancellationToken);
                items.Add(ListingPresentation.MapResponse(response, candidate));
            }

            return new PagedResult<ListingResponseDto>(items, pagination.Page, pagination.PageSize, totalCount);
        });

    public Task<PagedResult<ListingResponseDto>> GetMyResponsesAsync(
        PaginationRequest pagination, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            await paginationValidator.ValidateRequestAsync(pagination, cancellationToken);

            var totalCount = await listings.CountCandidateResponsesAsync(user.Id, cancellationToken);
            var responses = await listings.GetCandidateResponsesAsync(user.Id, pagination.Page, cancellationToken);
            if (responses.Count == 0)
            {
                return new PagedResult<ListingResponseDto>([], pagination.Page, pagination.PageSize, totalCount);
            }

            var card = await profiles.GetOwnCardAsync(user.Id, cancellationToken);
            var candidate = await presentation.MapCandidateAsync(user.Id, card, cancellationToken);
            var items = responses.Select(response => ListingPresentation.MapResponse(response, candidate)).ToArray();
            return new PagedResult<ListingResponseDto>(items, pagination.Page, pagination.PageSize, totalCount);
        });

    private async Task<(
        Domain.Listings.ListingResponse Response,
        Guid AuthorId,
        UserSearchItem? OwnCard,
        UserSearchItem? VisibleCard)> CreateResponseAsync(
        Guid listingId, Guid candidateId, string? message, CancellationToken cancellationToken)
    {
        try
        {
            return await transactions.ExecuteAsync(async token =>
            {
                var listing = await listings.GetForRespondingAsync(listingId, candidateId, token) ?? throw ListingAccess.NotFound();
                if (candidateId != listing.AuthorId)
                {
                    await pairLock.AcquireAsync(candidateId, listing.AuthorId, token);
                }

                if (!await access.CanInteractAsync(candidateId, listing.AuthorId, token))
                {
                    throw ListingAccess.NotFound();
                }

                var now = clock.UtcNow;
                var response = listing.SubmitResponse(Guid.NewGuid(), candidateId, message, now);
                listings.AddResponse(response);
                listings.RequireCurrentVersion(listing);
                notifications.Add(new Notification(
                    Guid.NewGuid(), listing.AuthorId, NotificationType.ListingResponseCreated, response.Id, now));

                var ownCard = await profiles.GetOwnCardAsync(candidateId, token);
                var visibleCard = await LoadVisibleCardAsync(listing.AuthorId, candidateId, token);
                return (response, listing.AuthorId, ownCard, visibleCard);
            }, cancellationToken);
        }
        catch (PersistenceException error) when (
            error.Code == PersistenceErrorCode.UniqueConstraintViolation &&
            error.ConstraintName == "ux_listing_responses_listing_candidate")
        {
            throw AppException.Conflict("listing.response.duplicate", "You have already responded to this listing.");
        }
    }

    private Task<ListingResponseDto> ReviewAsync(
        Guid responseId, NotificationType notificationType, CancellationToken cancellationToken) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            ListingAccess.ValidateId(responseId, "response");

            var saved = await transactions.ExecuteAsync(async token =>
            {
                var listing = await listings.GetByResponseIdAsync(responseId, token) ?? throw ResponseNotFound();
                ListingAccess.RequireOwner(listing.AuthorId, user.Id);

                var response = listing.Responses.SingleOrDefault(item => item.Id == responseId) ?? throw ResponseNotFound();
                await pairLock.AcquireAsync(user.Id, response.CandidateId, token);
                if (!await access.CanInteractAsync(user.Id, response.CandidateId, token))
                {
                    throw AppException.Forbidden("listing.response.candidate_unavailable", "The candidate is unavailable.");
                }

                var now = clock.UtcNow;
                if (notificationType == NotificationType.ListingResponseAccepted)
                {
                    listing.AcceptResponse(response.Id, now);
                    await conversations.EnsureBetweenAsync(
                        new Conversation(Guid.NewGuid(), listing.AuthorId, response.CandidateId,
                            ConversationSourceType.ListingResponse, response.Id, now),
                        token);
                }
                else
                {
                    listing.RejectResponse(response.Id, now);
                }

                listings.RequireCurrentVersion(listing);
                notifications.Add(new Notification(Guid.NewGuid(), response.CandidateId, notificationType, response.Id, now));
                var card = await LoadVisibleCardAsync(user.Id, response.CandidateId, token);
                return (Response: response, Card: card);
            }, cancellationToken);

            var candidate = await presentation.MapCandidateAsync(
                saved.Response.CandidateId, saved.Card, cancellationToken);
            var dto = ListingPresentation.MapResponse(saved.Response, candidate);
            await realtime.TrySendAsync(RealtimeEventNames.ListingResponseReviewed,
                token => notifier.NotifyListingResponseReviewedAsync(saved.Response.CandidateId, dto, token),
                cancellationToken);
            await unreadDelivery.PublishAsync(saved.Response.CandidateId, cancellationToken);
            return dto;
        });

    private async Task<UserSearchItem?> LoadVisibleCardAsync(
        Guid viewerId, Guid candidateId, CancellationToken cancellationToken)
    {
        var cards = await profiles.GetCardsAsync(viewerId, [candidateId], cancellationToken);
        return cards.SingleOrDefault();
    }

    private static AppException ResponseNotFound() =>
        AppException.NotFound("listing.response.not_found", "Listing response was not found.");
}
