using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using LabManagement.Protocol;

namespace LabManagement.Client
{
    public class Worker : BackgroundService
    {
        private const int HostPort = 5020;

        private readonly ILogger<Worker> _logger;
        private readonly IClientCommandDispatcher _commandDispatcher;
        private readonly ClientSharedSecretProvider _sharedSecretProvider;

        public Worker(
            ILogger<Worker> logger,
            IClientCommandDispatcher commandDispatcher,
            ClientSharedSecretProvider sharedSecretProvider)
        {
            _logger = logger;
            _commandDispatcher = commandDispatcher;
            _sharedSecretProvider = sharedSecretProvider;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            string? localIp = GetLocalIPv4();
            string? sharedSecret = _sharedSecretProvider.GetSharedSecret();

            if (localIp is null)
            {
                _logger.LogError("Tidak menemukan IPv4 aktif.");
                return;
            }

            if (string.IsNullOrWhiteSpace(sharedSecret))
            {
                _logger.LogError("Client Pairing Key belum dipasang.");
                return;
            }

            string hostIp = HostIpResolver.Resolve(
                localIp,
                _sharedSecretProvider.GetHostIpOverride());

            _logger.LogInformation(
                "Computer Name : {name}",
                Environment.MachineName);

            _logger.LogInformation(
                "Local IP      : {ip}",
                localIp);

            _logger.LogInformation(
                "Host IP       : {host}",
                hostIp);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ConnectToHostAsync(
                        hostIp,
                        localIp,
                        sharedSecret,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Koneksi ke Host gagal.");
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "Mencoba kembali dalam 5 detik...");

                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
                        stoppingToken);
                }
            }
        }

        private async Task ConnectToHostAsync(
            string hostIp,
            string localIp,
            string sharedSecret,
            CancellationToken cancellationToken)
        {
            using var client = new TcpClient();

            _logger.LogInformation(
                "Menghubungkan ke {host}:{port}...",
                hostIp,
                HostPort);

            await client.ConnectAsync(
                hostIp,
                HostPort,
                cancellationToken);

            _logger.LogInformation(
                "Berhasil terhubung ke Host.");

            using NetworkStream stream = client.GetStream();
            using var reader = new JsonLineReader(stream);
            using var writer = new JsonLineWriter(stream);
            using var connectionCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            CancellationToken connectionToken =
                connectionCancellation.Token;

            await writer.WriteAsync(
                RequestMessage.Create(
                    MessageType.Register,
                    new RegisterPayload
                    {
                        Hostname = Environment.MachineName,
                        IpAddress = localIp
                    }),
                connectionToken);

            _logger.LogInformation(
                "Registration berhasil dikirim.");

            await AuthenticateWithHostAsync(
                reader,
                writer,
                sharedSecret,
                connectionToken);

            Task receiveTask = ReceiveHostMessagesAsync(
                reader,
                writer,
                _commandDispatcher,
                connectionToken);

            try
            {
                while (!connectionToken.IsCancellationRequested)
                {
                    Task delayTask = Task.Delay(
                        TimeSpan.FromSeconds(2),
                        connectionToken);

                    if (await Task.WhenAny(receiveTask, delayTask) ==
                        receiveTask)
                    {
                        await receiveTask;
                        break;
                    }

                    await writer.WriteAsync(
                        RequestMessage.Create(MessageType.Heartbeat),
                        connectionToken);

                    _logger.LogInformation(
                        "Heartbeat dikirim: {time}",
                        DateTimeOffset.Now);
                }
            }
            finally
            {
                connectionCancellation.Cancel();

                try
                {
                    await receiveTask;
                }
                catch (OperationCanceledException)
                    when (connectionToken.IsCancellationRequested)
                {
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

                ResponseMessage response =
                    await commandDispatcher.DispatchAsync(
                        request,
                        cancellationToken);

                await writer.WriteAsync(response, cancellationToken);
            }
        }

        private static async Task AuthenticateWithHostAsync(
            JsonLineReader reader,
            JsonLineWriter writer,
            string sharedSecret,
            CancellationToken cancellationToken)
        {
            string? line = await reader.ReadLineAsync(cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            RequestMessage? challenge = line is null
                ? null
                : JsonSerializer.Deserialize<RequestMessage>(line, ProtocolJson.Options);

            if (challenge is null ||
                challenge.Type != MessageType.AuthChallenge ||
                string.IsNullOrWhiteSpace(challenge.RequestId) ||
                !challenge.TryGetPayload<AuthChallengePayload>(out AuthChallengePayload? payload) ||
                payload is null)
            {
                throw new InvalidDataException("Host tidak mengirim challenge autentikasi yang valid.");
            }

            string proof = SharedSecretAuthenticator.CreateProof(
                sharedSecret,
                payload.Challenge,
                Environment.MachineName);
            await writer.WriteAsync(
                ResponseMessage.CreateSuccess(
                    challenge.RequestId,
                    new AuthProofPayload { Proof = proof }),
                cancellationToken);
        }

        private static string? GetLocalIPv4()
        {
            foreach (NetworkInterface networkInterface
                     in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus !=
                    OperationalStatus.Up)
                {
                    continue;
                }

                if (networkInterface.NetworkInterfaceType ==
                    NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                IPInterfaceProperties properties =
                    networkInterface.GetIPProperties();

                foreach (UnicastIPAddressInformation address
                         in properties.UnicastAddresses)
                {
                    if (address.Address.AddressFamily !=
                        AddressFamily.InterNetwork ||
                        IPAddress.IsLoopback(address.Address))
                    {
                        continue;
                    }

                    byte[] bytes = address.Address.GetAddressBytes();

                    bool isPrivate =
                        bytes[0] == 10 ||
                        (bytes[0] == 172 &&
                         bytes[1] >= 16 &&
                         bytes[1] <= 31) ||
                        (bytes[0] == 192 && bytes[1] == 168);

                    if (isPrivate)
                        return address.Address.ToString();
                }
            }

            return null;
        }

    }
}
