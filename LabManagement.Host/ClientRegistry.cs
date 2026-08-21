using System.Collections.ObjectModel;

namespace LabManagement.Host
{
    public sealed class ClientRegistry
    {
        private readonly ObservableCollection<ClientInfo> _clients = new();

        public ReadOnlyObservableCollection<ClientInfo> Clients { get; }

        public ClientRegistry()
        {
            Clients = new ReadOnlyObservableCollection<ClientInfo>(
                _clients);
        }

        public ClientConnection? Register(ClientConnection connection)
        {
            ClientInfo? client = _clients.FirstOrDefault(
                x => string.Equals(
                    x.Hostname,
                    connection.Hostname,
                    StringComparison.OrdinalIgnoreCase));

            if (client is null)
            {
                client = new ClientInfo
                {
                    Hostname = connection.Hostname
                };

                client.Activate(connection);
                _clients.Add(client);
                return null;
            }

            ClientConnection? previousConnection = client.Connection;
            client.Activate(connection);
            return previousConnection;
        }

        public bool RecordHeartbeat(
            ClientConnection connection,
            DateTime heartbeat,
            out bool becameOnline)
        {
            ClientInfo? client = _clients.FirstOrDefault(
                x => ReferenceEquals(x.Connection, connection));

            becameOnline = client?.Status == "Offline";

            return client?.RecordHeartbeat(connection, heartbeat) == true;
        }

        public bool Disconnect(ClientConnection connection)
        {
            ClientInfo? client = _clients.FirstOrDefault(
                x => ReferenceEquals(x.Connection, connection));

            return client?.MarkDisconnected(connection) == true;
        }

        public bool MarkStaleClients(
            DateTime now,
            TimeSpan timeout)
        {
            bool statusChanged = false;

            foreach (ClientInfo client in _clients)
            {
                statusChanged |= client.MarkOfflineIfStale(
                    now,
                    timeout);
            }

            return statusChanged;
        }

        public IReadOnlyList<ClientConnection> DisconnectAll()
        {
            var activeConnections = new List<ClientConnection>();

            foreach (ClientInfo client in _clients)
            {
                if (client.Connection is { } connection &&
                    Disconnect(connection))
                {
                    activeConnections.Add(connection);
                }
            }

            return activeConnections;
        }

        public IReadOnlySet<string> GetConnectedIpAddresses() =>
            _clients
                .Where(client =>
                    client.Status == "Online" &&
                    client.Connection is not null)
                .Select(client => client.IpAddress)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        public int OnlineCount =>
            _clients.Count(x => x.Status == "Online");
    }
}
