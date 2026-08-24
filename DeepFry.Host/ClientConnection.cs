using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using DeepFry.Protocol;

namespace DeepFry.Host
{
    public sealed class ClientConnection
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly JsonLineReader _reader;
        private readonly JsonLineWriter _writer;
        private readonly ConcurrentDictionary<string,
            TaskCompletionSource<ResponseMessage>> _pendingResponses = new();

        public string Hostname { get; }
        public string IpAddress { get; }

        public DateTime LastHeartbeat { get; private set; }

        public event Action<ClientConnection, DateTime>? HeartbeatReceived;

        private ClientConnection(
            TcpClient client,
            NetworkStream stream,
            JsonLineReader reader,
            RegisterPayload registration)
        {
            _client = client;
            _stream = stream;
            _reader = reader;
            _writer = new JsonLineWriter(stream);

            Hostname = registration.Hostname;
            IpAddress = registration.IpAddress;

            LastHeartbeat = DateTime.Now;
        }

        public static async Task<ClientConnection?> AcceptAsync(
            TcpClient client,
            CancellationToken cancellationToken)
        {
            NetworkStream stream = client.GetStream();
            var reader = new JsonLineReader(stream);
            string? line;

            try
            {
                line = await reader.ReadLineAsync(cancellationToken);
            }
            catch (InvalidDataException)
            {
                reader.Dispose();
                client.Dispose();
                return null;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                reader.Dispose();
                client.Dispose();
                return null;
            }

            RequestMessage? message;

            try
            {
                message = JsonSerializer.Deserialize<RequestMessage>(
                    line,
                    ProtocolJson.Options);
            }
            catch (JsonException)
            {
                reader.Dispose();
                client.Dispose();
                return null;
            }

            if (message is null ||
                message.Type != MessageType.Register ||
                !message.TryGetPayload<RegisterPayload>(
                    out RegisterPayload? registration) ||
                registration is null ||
                string.IsNullOrWhiteSpace(registration.Hostname) ||
                string.IsNullOrWhiteSpace(registration.IpAddress))
            {
                reader.Dispose();
                client.Dispose();
                return null;
            }

            return new ClientConnection(
                client,
                stream,
                reader,
                registration);
        }

        public Task<ResponseMessage> SendCommandAsync(
            string commandName,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                throw new ArgumentException(
                    "Command name is required.",
                    nameof(commandName));

            var payload = new CommandRequestPayload
            {
                Name = commandName,
                Arguments = JsonSerializer.SerializeToElement(
                    new { },
                    ProtocolJson.Options)
            };

            return SendRequestAsync(
                RequestMessage.Create(MessageType.Command, payload),
                timeout,
                cancellationToken);
        }

        public async Task ListenAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string? line;

                    try
                    {
                        line = await _reader.ReadLineAsync(
                            cancellationToken);
                    }
                    catch (InvalidDataException)
                    {
                        break;
                    }

                    if (line is null)
                        break;

                    RequestMessage? message;

                    try
                    {
                        message = JsonSerializer.Deserialize<RequestMessage>(
                            line,
                            ProtocolJson.Options);
                    }
                    catch (JsonException)
                    {
                        continue;
                    }

                    if (message?.Type == MessageType.Heartbeat)
                    {
                        LastHeartbeat = DateTime.Now;
                        HeartbeatReceived?.Invoke(
                            this,
                            LastHeartbeat);
                        continue;
                    }

                    if (message?.Type != MessageType.Response)
                        continue;

                    ResponseMessage? response;

                    try
                    {
                        response = JsonSerializer.Deserialize<ResponseMessage>(
                            line,
                            ProtocolJson.Options);
                    }
                    catch (JsonException)
                    {
                        continue;
                    }

                    if (response is null ||
                        string.IsNullOrWhiteSpace(response.RequestId))
                    {
                        continue;
                    }

                    if (_pendingResponses.TryRemove(
                            response.RequestId,
                            out TaskCompletionSource<ResponseMessage>? pending))
                    {
                        pending.TrySetResult(response);
                    }
                }
            }
            finally
            {
                FailPendingResponses(
                    new IOException("Client connection was closed."));
            }
        }

        public void Dispose()
        {
            FailPendingResponses(
                new ObjectDisposedException(nameof(ClientConnection)));
            _writer.Dispose();
            _reader.Dispose();
            _stream.Dispose();
            _client.Dispose();
        }

        private async Task<ResponseMessage> SendRequestAsync(
            RequestMessage request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<ResponseMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_pendingResponses.TryAdd(request.RequestId, completion))
            {
                throw new InvalidOperationException(
                    $"Request ID is already pending: {request.RequestId}");
            }

            try
            {
                await _writer.WriteAsync(request, cancellationToken);
                return await completion.Task.WaitAsync(
                    timeout,
                    cancellationToken);
            }
            finally
            {
                _pendingResponses.TryRemove(request.RequestId, out _);
            }
        }

        private void FailPendingResponses(Exception exception)
        {
            foreach ((string requestId,
                      TaskCompletionSource<ResponseMessage> pending)
                     in _pendingResponses)
            {
                if (_pendingResponses.TryRemove(
                        requestId,
                        out _))
                {
                    pending.TrySetException(exception);
                }
            }
        }
    }
}
