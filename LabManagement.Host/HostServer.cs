using System.Net;
using System.Net.Sockets;

namespace LabManagement.Host
{
    public sealed class HostServer
    {
        private readonly TcpListener _listener;
        private string? _sharedSecret;

        public HostServer(int port, string? sharedSecret = null)
        {
            _listener = new TcpListener(
                IPAddress.Any,
                port);
            _sharedSecret = sharedSecret;
        }

        public void Start()
        {
            _listener.Start();
        }

        public async Task<ClientConnection?> AcceptClientAsync(
            CancellationToken cancellationToken)
        {
            TcpClient client =
                await _listener.AcceptTcpClientAsync(
                    cancellationToken);

            return await ClientConnection.AcceptAsync(
                client,
                _sharedSecret,
                cancellationToken);
        }

        public void SetSharedSecret(string sharedSecret)
        {
            _sharedSecret = sharedSecret;
        }

        public void Stop()
        {
            _listener.Stop();
        }
    }
}
