using FluentValidation;
using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Common;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Profiles;
using ITMO.Buddy.Application.Contracts.Search;
using ITMO.Buddy.Application.Errors;
using ITMO.Buddy.Application.Validation;
using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Application.Search;

internal sealed class UserSearchService(
    CurrentUserGuard currentUser,
    IValidator<SearchUsersRequest> validator,
    IUserSearchQueries queries,
    ProfileCardMapper mapper) : IUserSearchService
{
    public Task<PagedResult<ProfileCardResponse>> SearchAsync(
        SearchUsersRequest request,
        CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var viewer = await currentUser.RequireActiveUserAsync(cancellationToken);
            await validator.ValidateRequestAsync(request, cancellationToken);

            var page = await queries.SearchAsync(new UserSearchFilter
            {
                CurrentUserId = viewer.Id,
                Text = request.SearchText,
                Goals = request.GoalCategories?.Select(ContractEnum<GoalCategory>.Parse).ToArray(),
                InterestIds = request.InterestIds,
                SkillIds = request.SkillIds,
                FacultyIds = request.FacultyIds,
                Courses = request.Courses,
                Page = request.Page
            }, cancellationToken);

            var cards = new List<ProfileCardResponse>(page.Items.Count);
            foreach (var item in page.Items)
            {
                cards.Add(await mapper.MapAsync(item, cancellationToken));
            }

            return new PagedResult<ProfileCardResponse>(cards, page.Page, page.PageSize, page.TotalCount);
        });
}
