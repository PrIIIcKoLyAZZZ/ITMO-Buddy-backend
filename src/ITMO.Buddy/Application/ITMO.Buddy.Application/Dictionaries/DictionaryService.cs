using FluentValidation;
using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Contracts.Dictionaries;
using ITMO.Buddy.Application.Errors;
using ITMO.Buddy.Application.Validation;
using ITMO.Buddy.Domain.Users;

namespace ITMO.Buddy.Application.Dictionaries;

internal sealed class DictionaryService(
    IReferenceDataRepository referenceData,
    ITransactionRunner transactions,
    IValidator<DictionarySearchRequest> searchValidator,
    IValidator<CreateDictionaryItemRequest> createValidator) : IDictionaryService
{
    public Task<IReadOnlyList<DictionaryItemResponse>> GetFacultiesAsync(
        CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync<IReadOnlyList<DictionaryItemResponse>>(async () =>
        {
            var items = await referenceData.GetFacultiesAsync(cancellationToken);
            return items.Select(item => new DictionaryItemResponse(item.Id, item.Name)).ToArray();
        });

    public Task<IReadOnlyList<DictionaryItemResponse>> SearchInterestsAsync(
        DictionarySearchRequest request,
        CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync<IReadOnlyList<DictionaryItemResponse>>(async () =>
        {
            await searchValidator.ValidateRequestAsync(request, cancellationToken);
            var items = await referenceData.SearchInterestsAsync(request.SearchText, request.Limit, cancellationToken);
            return items.Select(item => new DictionaryItemResponse(item.Id, item.Name)).ToArray();
        });

    public Task<IReadOnlyList<DictionaryItemResponse>> SearchSkillsAsync(
        DictionarySearchRequest request,
        CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync<IReadOnlyList<DictionaryItemResponse>>(async () =>
        {
            await searchValidator.ValidateRequestAsync(request, cancellationToken);
            var items = await referenceData.SearchSkillsAsync(request.SearchText, request.Limit, cancellationToken);
            return items.Select(item => new DictionaryItemResponse(item.Id, item.Name)).ToArray();
        });

    public Task<DictionaryItemResponse> CreateInterestAsync(
        CreateDictionaryItemRequest request,
        CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            await createValidator.ValidateRequestAsync(request, cancellationToken);
            var candidate = new Interest(Guid.NewGuid(), request.Name);
            var item = await transactions.ExecuteAsync(
                token => referenceData.GetOrCreateAsync(candidate, token), cancellationToken);
            return new DictionaryItemResponse(item.Id, item.Name);
        });

    public Task<DictionaryItemResponse> CreateSkillAsync(
        CreateDictionaryItemRequest request,
        CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            await createValidator.ValidateRequestAsync(request, cancellationToken);
            var candidate = new Skill(Guid.NewGuid(), request.Name);
            var item = await transactions.ExecuteAsync(
                token => referenceData.GetOrCreateAsync(candidate, token), cancellationToken);
            return new DictionaryItemResponse(item.Id, item.Name);
        });
}
