using DeepFry.Protocol;

namespace DeepFry.Client;

public interface ISystemPowerManager
{
    Task<CommandResultPayload> RestartAsync(
        CancellationToken cancellationToken);
}
