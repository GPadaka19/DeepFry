using DeepFry.Protocol;

namespace DeepFry.Client;

public interface IClientCommandDispatcher
{
    Task<ResponseMessage> DispatchAsync(
        RequestMessage request,
        CancellationToken cancellationToken);
}
