using LabManagement.Protocol;

namespace LabManagement.Client;

public interface IClientCommandDispatcher
{
    Task<ResponseMessage> DispatchAsync(
        RequestMessage request,
        CancellationToken cancellationToken);
}
