using DeepFry.Protocol;

namespace DeepFry.Client;

public interface IUwfManager
{
    Task<UwfStatusPayload> GetStatusAsync(
        CancellationToken cancellationToken);

    Task<CommandResultPayload> LockDriveCAsync(
        CancellationToken cancellationToken);

    Task<CommandResultPayload> UnlockDriveCAsync(
        CancellationToken cancellationToken);
}
