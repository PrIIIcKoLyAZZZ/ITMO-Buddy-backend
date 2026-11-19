using FluentValidation;
using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Abstractions.Time;
using ITMO.Buddy.Application.Common;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Listings;
using ITMO.Buddy.Application.Errors;
using ITMO.Buddy.Application.Validation;
using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Domain.Listings;
using ITMO.Buddy.Domain.Users;
using ListingDetails = ITMO.Buddy.Application.Contracts.Listings.ListingResponse;

namespace ITMO.Buddy.Application.Listings;

internal sealed class ListingService(
    CurrentUserGuard currentUser,
    IListingRepository listings,
    IListingSearchQueries queries,
    IReferenceDataRepository referenceData,
    ITransactionRunner transactions,
    IDateTimeProvider clock,
    ListingAccess access,
    ListingPresentation presentation,
    IValidator<CreateListingRequest> createValidator,
    IValidator<UpdateListingRequest> updateValidator,
    IValidator<SearchListingsRequest> searchValidator,
    IValidator<PaginationRequest> paginationValidator) : IListingService
{
    public Task<ListingDetails> CreateAsync(CreateListingRequest request, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            await createValidator.ValidateRequestAsync(request, cancellationToken);

            var saved = await transactions.ExecuteAsync(async token =>
            {
                var skills = await LoadSkillsAsync(request.RequiredSkillIds, token);
                var now = clock.UtcNow;
                var listing = new Listing(Guid.NewGuid(), user.Id, request.Title, request.Description,
                    ContractEnum<GoalCategory>.Parse(request.Category), request.RequiredParticipantsCount,
                    ContractEnum<ListingFormat>.Parse(request.Format), now, request.DeadlineUtc, request.EventAtUtc);

                listing.SetRequiredSkills(skills, now);
                listings.Add(listing);
                return (Listing: listing, Author: await LoadAuthorAsync(user.Id, user.Id, token));
            }, cancellationToken);

            var author = await presentation.MapAuthorAsync(user.Id, saved.Author, cancellationToken);
            return ListingPresentation.MapDetails(saved.Listing, author);
        });

    public Task<ListingDetails> UpdateAsync(Guid listingId, UpdateListingRequest request, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            ListingAccess.ValidateId(listingId, "listing");
            await updateValidator.ValidateRequestAsync(request, cancellationToken);

            var saved = await transactions.ExecuteAsync(async token =>
            {
                var listing = await LoadOwnedAsync(listingId, user.Id, token);
                var skills = await LoadSkillsAsync(request.RequiredSkillIds, token);
                var now = clock.UtcNow;

                listing.UpdateDetails(request.Title, request.Description, ContractEnum<GoalCategory>.Parse(request.Category),
                    request.RequiredParticipantsCount, ContractEnum<ListingFormat>.Parse(request.Format),
                    request.DeadlineUtc, request.EventAtUtc, now);
                listing.SetRequiredSkills(skills, now);
                listings.RequireCurrentVersion(listing);
                return (Listing: listing, Author: await LoadAuthorAsync(user.Id, user.Id, token));
            }, cancellationToken);

            var author = await presentation.MapAuthorAsync(user.Id, saved.Author, cancellationToken);
            return ListingPresentation.MapDetails(saved.Listing, author);
        });

    public Task DeleteAsync(Guid listingId, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            ListingAccess.ValidateId(listingId, "listing");

            await transactions.ExecuteAsync(async token =>
            {
                var listing = await LoadOwnedAsync(listingId, user.Id, token);
                await listings.RemoveResponseNotificationsAsync(listing.Id, token);
                listings.Remove(listing);
            }, cancellationToken);
        });

    public Task CloseAsync(Guid listingId, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            ListingAccess.ValidateId(listingId, "listing");

            await transactions.ExecuteAsync(async token =>
            {
                var listing = await LoadOwnedAsync(listingId, user.Id, token);
                listing.Close(clock.UtcNow);
            }, cancellationToken);
        });

    public Task<ListingDetails> GetByIdAsync(Guid listingId, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            ListingAccess.ValidateId(listingId, "listing");
            var listing = await listings.GetDetailsAsync(listingId, cancellationToken) ?? throw ListingAccess.NotFound();

            if (listing.AuthorId != user.Id &&
                (!listing.IsAvailableAt(clock.UtcNow) ||
                 !await access.CanInteractAsync(user.Id, listing.AuthorId, cancellationToken)))
            {
                throw ListingAccess.NotFound();
            }

            var authorInfo = await LoadAuthorAsync(user.Id, listing.AuthorId, cancellationToken);
            var author = await presentation.MapAuthorAsync(listing.AuthorId, authorInfo, cancellationToken);
            return ListingPresentation.MapDetails(listing, author);
        });

    public Task<PagedResult<ListingCardResponse>> SearchAsync(SearchListingsRequest request, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            await searchValidator.ValidateRequestAsync(request, cancellationToken);
            var page = await queries.SearchPublicAsync(new ListingSearchFilter
            {
                ViewerId = user.Id,
                NowUtc = clock.UtcNow,
                Text = request.SearchText,
                Category = request.Category is { } category ? ContractEnum<GoalCategory>.Parse(category) : null,
                Format = request.Format is { } format ? ContractEnum<ListingFormat>.Parse(format) : null,
                SkillIds = request.SkillIds,
                Page = request.Page
            }, cancellationToken);

            return await MapPageAsync(user.Id, page, cancellationToken);
        });

    public Task<PagedResult<ListingCardResponse>> GetMyListingsAsync(PaginationRequest pagination, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            await paginationValidator.ValidateRequestAsync(pagination, cancellationToken);
            var page = await queries.GetByAuthorAsync(user.Id, pagination.Page, cancellationToken);
            return await MapPageAsync(user.Id, page, cancellationToken);
        });

    private async Task<Listing> LoadOwnedAsync(Guid listingId, Guid viewerId, CancellationToken cancellationToken)
    {
        var listing = await listings.GetForEditingAsync(listingId, cancellationToken) ?? throw ListingAccess.NotFound();
        ListingAccess.RequireOwner(listing.AuthorId, viewerId);
        return listing;
    }

    private async Task<IReadOnlyList<Skill>> LoadSkillsAsync(IReadOnlyList<Guid> skillIds, CancellationToken cancellationToken)
    {
        var skills = await referenceData.GetSkillsByIdsAsync(skillIds, cancellationToken);
        if (skills.Count != skillIds.Count)
        {
            throw AppException.Validation("listing.skill_not_found", "One or more required skills do not exist.");
        }

        return skills;
    }

    private async Task<ListingAuthorItem?> LoadAuthorAsync(Guid viewerId, Guid authorId, CancellationToken cancellationToken)
    {
        var authors = await queries.GetAuthorsAsync(viewerId, [authorId], cancellationToken);
        return authors.SingleOrDefault();
    }

    private async Task<PagedResult<ListingCardResponse>> MapPageAsync(
        Guid viewerId, QueryPage<ListingSearchItem> page, CancellationToken cancellationToken)
    {
        var authorIds = page.Items.Select(item => item.AuthorId).Distinct().ToArray();
        var authors = new Dictionary<Guid, ListingAuthorResponse>(authorIds.Length);
        if (authorIds.Length > 0)
        {
            var profiles = (await queries.GetAuthorsAsync(viewerId, authorIds, cancellationToken))
                .ToDictionary(item => item.UserId);

            foreach (var authorId in authorIds)
            {
                authors.Add(authorId, await presentation.MapAuthorAsync(
                    authorId, profiles.GetValueOrDefault(authorId), cancellationToken));
            }
        }

        return page.ToResponse(item => ListingPresentation.MapListingCard(item, authors[item.AuthorId]));
    }
}
