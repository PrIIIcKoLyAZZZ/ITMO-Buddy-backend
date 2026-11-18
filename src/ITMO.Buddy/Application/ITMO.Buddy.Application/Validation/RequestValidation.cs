using FluentValidation;
using ITMO.Buddy.Application.Errors;
using ContractValidationError = ITMO.Buddy.Application.Contracts.Common.ValidationError;

namespace ITMO.Buddy.Application.Validation;

internal static class RequestValidation
{
    public static async Task ValidateRequestAsync<T>(
        this IValidator<T> validator,
        T request,
        CancellationToken cancellationToken)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request is null)
        {
            throw AppException.Validation("request.required", "A request is required.");
        }

        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(error => error.PropertyName)
                .Select(group => new ContractValidationError(
                    group.Key,
                    group.Select(error => error.ErrorMessage).Distinct().ToArray()))
                .ToArray();

            throw AppException.Validation("request.invalid", "Request validation failed.", errors);
        }
    }
}
