using LabManagement.Protocol;

namespace LabManagement.Client;

public interface IUwfManager
{
    Task<UwfStatusPayload> GetStatusAsync(
        CancellationToken cancellationToken);

    Task<CommandResultPayload> LockDriveCAsync(
        CancellationToken cancellationToken);

    Task<CommandResultPayload> UnlockDriveCAsync(
        CancellationToken cancellationToken);
}
