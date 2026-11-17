namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

internal static class RepositoryPaging
{
    public const int PageSize = 20;

    public const int MaxBatchSize = 500;

    public static int GetSkip(int page)
    {
        if (page < 1 || page > int.MaxValue / PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                "Page number is out of range.");
        }

        return (page - 1) * PageSize;
    }

    public static void ValidateBatchSize(int take)
    {
        if (take < 1 || take > MaxBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                $"Batch size must be between 1 and {MaxBatchSize}.");
        }
    }
}