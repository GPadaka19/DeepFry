using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;

namespace DeepFry.Host;

public sealed class HostServer
{
    private static readonly TimeSpan ConnectionTimeout =
        TimeSpan.FromMilliseconds(750);
    private readonly int _port;

    public HostServer(int port = LabNetworkDiscovery.ClientPort)
    {
        _port = port;
    }

    public async Task<IReadOnlyList<ClientConnection>> DiscoverClientsAsync(
        IEnumerable<string> candidateAddresses,
        IReadOnlySet<string> connectedAddresses,
        CancellationToken cancellationToken)
    {
        var connections = new ConcurrentBag<ClientConnection>();
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = 16
        };

        await Parallel.ForEachAsync(
            candidateAddresses.Distinct(StringComparer.OrdinalIgnoreCase),
            options,
            async (address, token) =>
            {
                if (connectedAddresses.Contains(address))
                    return;

                ClientConnection? connection = await TryConnectAsync(
                    address,
                    token);

                if (connection is not null)
                    connections.Add(connection);
            });

        return connections.ToArray();
    }

    private async Task<ClientConnection?> TryConnectAsync(
        string address,
        CancellationToken cancellationToken)
    {
        var client = new TcpClient(AddressFamily.InterNetwork);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(ConnectionTimeout);

        try
        {
            await client.ConnectAsync(address, _port, timeout.Token);
            return await ClientConnection.AcceptAsync(client, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            client.Dispose();
            return null;
        }
        catch (SocketException)
        {
            client.Dispose();
            return null;
        }
        catch (IOException)
        {
            client.Dispose();
            return null;
        }
    }
}
