using Microsoft.Extensions.Logging;

namespace ITMO.Buddy.Application.Common;

internal sealed class RealtimeDelivery(ILogger<RealtimeDelivery> logger)
{
    public async Task TrySendAsync(
        string eventName,
        Func<CancellationToken, Task> publish,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await publish(cancellationToken);
            }
        }
        catch (Exception error)
        {
            try
            {
                logger.LogWarning("Realtime event {EventName} was not delivered ({ErrorType}).",
                    eventName, error.GetType().Name);
            }
            catch (Exception)
            {
                // A failing log provider must not change the result of a committed write.
            }
        }
    }
}
