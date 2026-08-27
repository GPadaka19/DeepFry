using DeepFry.Protocol;

namespace DeepFry.Client;

public sealed class ClientCommandDispatcher : IClientCommandDispatcher
{
    private readonly IUwfManager _uwfManager;
    private readonly ISystemPowerManager _systemPowerManager;

    public ClientCommandDispatcher(
        IUwfManager uwfManager,
        ISystemPowerManager systemPowerManager)
    {
        _uwfManager = uwfManager;
        _systemPowerManager = systemPowerManager;
    }

    public async Task<ResponseMessage> DispatchAsync(
        RequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Type != MessageType.Command ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            !request.TryGetPayload<CommandRequestPayload>(
                out CommandRequestPayload? command) ||
            string.IsNullOrWhiteSpace(command?.Name))
        {
            return ResponseMessage.CreateError(
                request.RequestId,
                new ErrorInfo
                {
                    Code = "INVALID_COMMAND",
                    Message = "Command request is invalid."
                });
        }

        try
        {
            return command.Name switch
            {
                "uwf.status" => ResponseMessage.CreateSuccess(
                    request.RequestId,
                    await _uwfManager.GetStatusAsync(cancellationToken)),
                "uwf.lock" => ResponseMessage.CreateSuccess(
                    request.RequestId,
                    await _uwfManager.LockAsync(cancellationToken)),
                "uwf.unlock" => ResponseMessage.CreateSuccess(
                    request.RequestId,
                    await _uwfManager.UnlockAsync(cancellationToken)),
                "system.restart" => ResponseMessage.CreateSuccess(
                    request.RequestId,
                    await _systemPowerManager.RestartAsync(cancellationToken)),
                _ => ResponseMessage.CreateError(
                    request.RequestId,
                    new ErrorInfo
                    {
                        Code = "COMMAND_NOT_ALLOWED",
                        Message = "Command is not in the allowlist."
                    })
            };
        }
        catch (Exception ex) when (
            ex is InvalidOperationException ||
            ex is System.ComponentModel.Win32Exception ||
            ex is System.IO.IOException)
        {
            return ResponseMessage.CreateError(
                request.RequestId,
                new ErrorInfo
                {
                    Code = command.Name.StartsWith(
                        "uwf.",
                        StringComparison.Ordinal)
                        ? "UWF_COMMAND_FAILED"
                        : "SYSTEM_COMMAND_FAILED",
                    Message = ex.Message
                });
        }
    }
}
