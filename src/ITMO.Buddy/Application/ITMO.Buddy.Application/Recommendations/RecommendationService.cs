using FluentValidation;
using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Application.Abstractions.Recommendations;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Abstractions.Time;
using ITMO.Buddy.Application.Common;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Recommendations;
using ITMO.Buddy.Application.Errors;
using ITMO.Buddy.Application.Search;
using ITMO.Buddy.Application.Validation;
using ITMO.Buddy.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ITMO.Buddy.Application.Recommendations;

internal sealed class RecommendationService(
    CurrentUserGuard currentUser,
    IUserProfileRepository profiles,
    IRecommendationCandidateQueries candidates,
    IUserSearchQueries cards,
    IRecommendationSkipStore skips,
    IDateTimeProvider clock,
    IOptions<RecommendationOptions> options,
    IValidator<GetRecommendationsRequest> validator,
    RecommendationScorer scorer,
    ProfileCardMapper cardMapper) : IRecommendationService
{
    public Task<PagedResult<RecommendationResponse>> GetRecommendationsAsync(
        GetRecommendationsRequest request,
        CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            await validator.ValidateRequestAsync(request, cancellationToken);

            var viewer = await profiles.GetByUserIdAsync(user.Id, cancellationToken);
            if (viewer is not { IsComplete: true })
            {
                return new PagedResult<RecommendationResponse>(
                    Array.Empty<RecommendationResponse>(), request.Page, request.PageSize, 0);
            }

            var candidateRequest = new RecommendationCandidateRequest
            {
                CurrentUserId = user.Id,
                GoalFilter = request.GoalCategory is { } goal
                    ? ContractEnum<GoalCategory>.Parse(goal)
                    : null
            };
            var now = clock.UtcNow;
            var score = scorer.CompareWith(viewer);
            var scored = new List<ScoredCandidate>();
            while (true)
            {
                var batch = await candidates.GetCandidatesAsync(candidateRequest, cancellationToken);
                if (batch.Count == 0)
                {
                    break;
                }

                var skipped = await skips.GetActiveSkippedIdsAsync(
                    user.Id, batch.Select(item => item.UserId).ToArray(), now, cancellationToken);

                foreach (var candidate in batch)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!skipped.Contains(candidate.UserId))
                    {
                        scored.Add(score(candidate));
                    }
                }

                if (batch.Count < candidateRequest.Limit)
                {
                    break;
                }

                candidateRequest = candidateRequest with { AfterProfileId = batch[^1].ProfileId };
            }

            var selected = scored
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.UserId)
                .Skip(Pagination.GetOffset(request.Page))
                .Take(request.PageSize)
                .ToArray();

            if (selected.Length == 0)
            {
                return new PagedResult<RecommendationResponse>(
                    Array.Empty<RecommendationResponse>(), request.Page, request.PageSize, scored.Count);
            }

            var pageCards = await cards.GetCardsAsync(
                user.Id, selected.Select(item => item.UserId).ToArray(), cancellationToken);
            var byUser = pageCards.ToDictionary(item => item.UserId);

            var items = new List<RecommendationResponse>(selected.Length);
            foreach (var item in selected)
            {
                if (byUser.TryGetValue(item.UserId, out var card))
                {
                    items.Add(new RecommendationResponse(
                        await cardMapper.MapAsync(card, cancellationToken), item.Score, item.GetReasons()));
                }
            }

            return new PagedResult<RecommendationResponse>(items, request.Page, request.PageSize, scored.Count);
        });

    public Task SkipRecommendationAsync(Guid targetUserId, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var user = await currentUser.RequireActiveUserAsync(cancellationToken);
            if (targetUserId == Guid.Empty || targetUserId == user.Id)
            {
                throw AppException.Validation("recommendation.invalid_target", "A different user ID is required.");
            }

            var target = await cards.GetCardsAsync(user.Id, [targetUserId], cancellationToken);
            if (target.Count == 0)
            {
                throw AppException.NotFound("recommendation.target_not_found", "The recommended user was not found.");
            }

            var now = clock.UtcNow;
            var duration = options.Value.SkipDuration;
            var expiresAt = duration > DateTimeOffset.MaxValue - now ? DateTimeOffset.MaxValue : now + duration;
            await skips.SkipAsync(user.Id, targetUserId, expiresAt, cancellationToken);
        });
}
