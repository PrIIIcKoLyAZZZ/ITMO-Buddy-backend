using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Common;

namespace ITMO.Buddy.Application.Errors;

internal static class ApplicationOperation
{
    public static async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception error) when (error is DomainException or PersistenceException)
        {
            throw new AppException(ApplicationErrorMapper.Map(error));
        }
    }

    public static Task RunAsync(Func<Task> operation) =>
        RunAsync(async () =>
        {
            await operation();
            return true;
        });
}
