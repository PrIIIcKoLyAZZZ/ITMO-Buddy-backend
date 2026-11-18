using ITMO.Buddy.Application.Abstractions.Storage;

namespace ITMO.Buddy.Application.Common;

internal sealed class OptionalPhotoUrl(IFileStorage files)
{
    public async Task<string?> GetAsync(string? objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (objectKey is null)
        {
            return null;
        }

        try
        {
            return await files.GetReadUrlAsync(objectKey, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
