using LabManagement.Protocol;

namespace LabManagement.Client;

public interface ISystemPowerManager
{
    Task<CommandResultPayload> RestartAsync(
        CancellationToken cancellationToken);
}
