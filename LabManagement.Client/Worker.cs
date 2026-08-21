using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace LabManagement.Client;

public sealed class Worker : BackgroundService
{
    public const int ListenPort = 5020;

    private readonly ILogger<Worker> _logger;
    private readonly IClientCommandDispatcher _commandDispatcher;

    public Worker(
        ILogger<Worker> logger,
        IClientCommandDispatcher commandDispatcher)
    {
        _logger = logger;
        _commandDispatcher = commandDispatcher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LegacyClientSettingsCleaner.RemovePairingKeyFile(_logger);
        await ClientFirewallConfigurator.EnsureInboundRuleAsync(
            _logger,
            stoppingToken);

        var listener = new TcpListener(IPAddress.Any, ListenPort);
        var sessions = new ConcurrentDictionary<int, Task>();
        int sessionId = 0;

        try
        {
            listener.Start();
            _logger.LogInformation(
                "LabManagement Client aktif pada TCP port {port}. Hostname: {hostname}",
                ListenPort,
                Environment.MachineName);

            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient host = await listener.AcceptTcpClientAsync(stoppingToken);
                int currentSessionId = Interlocked.Increment(ref sessionId);
                Task session = RunSessionSafelyAsync(host, stoppingToken);
                sessions[currentSessionId] = session;

                _ = session.ContinueWith(
                    completedSession =>
                        sessions.TryRemove(currentSessionId, out _),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Stop();

            try
            {
                await Task.WhenAll(sessions.Values);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }

    private async Task RunSessionSafelyAsync(
        TcpClient host,
        CancellationToken cancellationToken)
    {
        string remoteAddress =
            (host.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ??
            "unknown";

        try
        {
            _logger.LogInformation(
                "Host tersambung dari {address}.",
                remoteAddress);
            await ClientSession.RunAsync(
                host,
                _commandDispatcher,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Sesi Host {address} berakhir.",
                remoteAddress);
        }
    }
}
