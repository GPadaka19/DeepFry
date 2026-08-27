using DeepFry.Protocol;

namespace DeepFry.Client;

public interface IUwfManager
{
    Task<UwfStatusPayload> GetStatusAsync(
        CancellationToken cancellationToken);

    Task<CommandResultPayload> LockAsync(
        CancellationToken cancellationToken);

    Task<CommandResultPayload> UnlockAsync(
        CancellationToken cancellationToken);
}
