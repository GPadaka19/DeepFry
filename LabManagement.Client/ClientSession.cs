using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using LabManagement.Protocol;

namespace LabManagement.Client;

public static class ClientSession
{
    public static async Task RunAsync(
        TcpClient host,
        IClientCommandDispatcher commandDispatcher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(commandDispatcher);

        using (host)
        using (NetworkStream stream = host.GetStream())
        using (var reader = new JsonLineReader(stream))
        using (var writer = new JsonLineWriter(stream))
        using (var sessionCancellation =
               CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            string localIp =
                (host.Client.LocalEndPoint as IPEndPoint)?.Address.ToString() ??
                IPAddress.Loopback.ToString();

            await writer.WriteAsync(
                RequestMessage.Create(
                    MessageType.Register,
                    new RegisterPayload
                    {
                        Hostname = Environment.MachineName,
                        IpAddress = localIp
                    }),
                sessionCancellation.Token);

            Task receiveTask = ReceiveHostMessagesAsync(
                reader,
                writer,
                commandDispatcher,
                sessionCancellation.Token);

            try
            {
                while (!sessionCancellation.IsCancellationRequested)
                {
                    await writer.WriteAsync(
                        RequestMessage.Create(MessageType.Heartbeat),
                        sessionCancellation.Token);

                    Task delayTask = Task.Delay(
                        TimeSpan.FromSeconds(2),
                        sessionCancellation.Token);

                    if (await Task.WhenAny(receiveTask, delayTask) == receiveTask)
                    {
                        await receiveTask;
                        break;
                    }
                }
            }
            finally
            {
                sessionCancellation.Cancel();

                try
                {
                    await receiveTask;
                }
                catch (OperationCanceledException)
                    when (sessionCancellation.IsCancellationRequested)
                {
                }
            }
        }
    }

    private static async Task ReceiveHostMessagesAsync(
        JsonLineReader reader,
        JsonLineWriter writer,
        IClientCommandDispatcher commandDispatcher,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;

            try
            {
                line = await reader.ReadLineAsync(cancellationToken);
            }
            catch (InvalidDataException)
            {
                break;
            }

            if (line is null)
                break;

            RequestMessage? request;

            try
            {
                request = JsonSerializer.Deserialize<RequestMessage>(
                    line,
                    ProtocolJson.Options);
            }
            catch (JsonException)
            {
                continue;
            }

            if (request?.Type != MessageType.Command ||
                string.IsNullOrWhiteSpace(request.RequestId))
            {
                continue;
            }

            ResponseMessage response = await commandDispatcher.DispatchAsync(
                request,
                cancellationToken);
            await writer.WriteAsync(response, cancellationToken);
        }
    }
}
