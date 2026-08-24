using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DeepFry.Client;
using DeepFry.Host;
using DeepFry.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

if (args.Contains("--ui-smoke", StringComparer.Ordinal))
{
    TestMainWindowConstruction();
    Console.WriteLine("PASS: MainWindow construction.");
}
else if (args.Contains("--ui-startup", StringComparer.Ordinal))
{
    TestMainWindowStartup();
    Console.WriteLine("PASS: MainWindow startup.");
}
else if (args.Contains("--ui-layout", StringComparer.Ordinal))
{
    TestMainWindowLayout();
    Console.WriteLine("PASS: MainWindow layout.");
}
else
{
    TestProtocolContract();
    TestLabNetworkDiscovery();
    TestAppKeepsRunningAfterLoginDialogCloses();
    TestSignInDialogHasOnePasswordField();
    TestPasswordSetupSubmission();
    TestHostPasswordManager();
    TestHostDiagnosticLog();
    TestClientDiagnosticLog();
    TestUwfStatusResultFormatting();
    TestUwfStatusColumnFormatting();
    TestUwfStatusParserUsesCurrentDriveCVolumeState();
    TestUwfStatusParserHandlesConsoleControlCharacters();
    await TestUwfSimulationFixtureAsync();
    await TestCommandDispatcherAsync();
    await TestHostDiscoversClientAsync();
    await TestHeartbeatTimeoutAndDisconnectAsync();
    await TestDuplicateHostnameReconnectAsync();
    await TestMalformedRegistrationAsync();
    await TestMalformedAndFramedMessagesAsync();
    await TestIncompleteMessageEofAsync();
    await TestMultipleRequestResponseAsync();
    await TestRequestTimeoutAndDisconnectAsync();

    Console.WriteLine(
        "PASS: discovery, lifecycle, reconnect, command, EOF, and framing checks.");
}

static void TestLabNetworkDiscovery()
{
    IReadOnlyList<string> addresses =
        LabNetworkDiscovery.BuildClientAddresses("10.22.4.90");

    Assert(
        addresses.Count == 89 &&
        addresses[0] == "10.22.4.1" &&
        addresses[^1] == "10.22.4.89" &&
        !addresses.Contains("10.22.4.90") &&
        LabNetworkDiscovery.BuildClientAddresses("10.22.4.13").Count == 0,
        "Host scan range did not follow the 10.x.x.90 lab convention.");
}

static void TestAppKeepsRunningAfterLoginDialogCloses()
{
    Exception? failure = null;

    var thread = new Thread(() =>
    {
        try
        {
            var application = new App();
            Assert(
                application.ShutdownMode == ShutdownMode.OnExplicitShutdown,
                "Host shuts down when the login dialog closes before MainWindow is shown.");
            application.Shutdown();
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
        throw failure;
}

static void TestSignInDialogHasOnePasswordField()
{
    Exception? failure = null;

    var thread = new Thread(() =>
    {
        try
        {
            var dialog = new PasswordDialog(PasswordDialogMode.SignIn);
            Assert(
                CountPasswordBoxes(dialog) == 1,
                "Login dialog must contain exactly one password field.");
            dialog.Close();
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
        throw failure;
}

static int CountPasswordBoxes(DependencyObject root)
{
    int count = root is PasswordBox ? 1 : 0;

    foreach (object child in LogicalTreeHelper.GetChildren(root))
    {
        if (child is DependencyObject dependencyObject)
            count += CountPasswordBoxes(dependencyObject);
    }

    return count;
}

static void TestPasswordSetupSubmission()
{
    Assert(
        PasswordDialog.HasRequiredPassword(
            PasswordDialogMode.Setup,
            currentPassword: null,
            newPassword: "L4B244"),
        "Setup password is rejected even though a valid new password was entered.");
}

static void TestHostPasswordManager()
{
    string directory = Path.Combine(
        Path.GetTempPath(),
        "DeepFry-PasswordTests-" + Guid.NewGuid().ToString("N"));

    try
    {
        var manager = new HostPasswordManager(directory);
        Assert(
            manager.Status == PasswordConfigurationStatus.NotConfigured,
            "Fresh password configuration was not detected.");

        manager.SetPassword("L4B244");

        Assert(
            manager.Status == PasswordConfigurationStatus.Ready &&
            manager.VerifyPassword("L4B244") &&
            !manager.VerifyPassword("wrong-password"),
            "Host password verification is incorrect.");

        string settingsPath = Path.Combine(directory, "host-settings.json");
        string settingsJson = File.ReadAllText(settingsPath);
        Assert(
            !settingsJson.Contains("L4B244", StringComparison.Ordinal) &&
            settingsJson.Contains("PasswordHash", StringComparison.Ordinal) &&
            settingsJson.Contains("PasswordSalt", StringComparison.Ordinal),
            "Host settings persisted the password instead of only its hash data.");

        int objectStart = settingsJson.IndexOf('{');
        File.WriteAllText(
            settingsPath,
            settingsJson.Insert(
                objectStart + 1,
                "\n  \"ClientSharedSecret\": \"legacy-key\",\n  \"TcpPort\": 6000,"));
        manager.GetConfiguration();
        string migratedSettingsJson = File.ReadAllText(settingsPath);
        Assert(
            !migratedSettingsJson.Contains(
                "ClientSharedSecret",
                StringComparison.OrdinalIgnoreCase) &&
            !migratedSettingsJson.Contains(
                "TcpPort",
                StringComparison.OrdinalIgnoreCase),
            "Legacy pairing key or TCP port survived Host settings migration.");

        manager.SaveConfiguration(new HostConfiguration("Lab 244"));

        Assert(
            !manager.ChangePassword("wrong-password", "LAB999") &&
            manager.ChangePassword("L4B244", "LAB999") &&
            !manager.VerifyPassword("L4B244") &&
            manager.VerifyPassword("LAB999") &&
            manager.GetConfiguration() == new HostConfiguration("Lab 244"),
            "Host password change failed or reset the saved lab configuration.");
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}

static void TestMainWindowConstruction()
{
    Exception? failure = null;

    var thread = new Thread(() =>
    {
        try
        {
            var window = new MainWindow();
            window.Close();

            var signInDialog = new PasswordDialog(PasswordDialogMode.SignIn);
            signInDialog.Close();
            var setupDialog = new PasswordDialog(PasswordDialogMode.Setup);
            setupDialog.Close();
            var changeDialog = new PasswordDialog(PasswordDialogMode.Change);
            changeDialog.Close();
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
        throw new InvalidOperationException(
            "MainWindow construction failed.",
            failure);
}

static void TestUwfStatusResultFormatting()
{
    Assert(
        MainWindow.FormatUwfStatusResult(UwfState.Locked) == "On" &&
        MainWindow.FormatUwfStatusResult(UwfState.Unlocked) == "Off" &&
        MainWindow.FormatUwfStatusResult(UwfState.Unknown) == "Unknown",
        "UWF status result was not reduced to On, Off, or Unknown.");
}

static void TestHostDiagnosticLog()
{
    string directory = Path.Combine(
        Path.GetTempPath(),
        $"DeepFry-log-test-{Guid.NewGuid():N}");

    try
    {
        var log = new HostDiagnosticLog(directory);
        log.Write(
            "UWF status response",
            "State=Unknown\nRaw output=Volume state: Un-protected");

        string contents = File.ReadAllText(log.LogPath);

        Assert(
            contents.Contains("UWF status response", StringComparison.Ordinal) &&
            contents.Contains("Volume state: Un-protected", StringComparison.Ordinal),
            "Host diagnostic log did not preserve the UWF response details.");
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}

static void TestClientDiagnosticLog()
{
    string directory = Path.Combine(
        Path.GetTempPath(),
        $"DeepFry-log-test-{Guid.NewGuid():N}");

    try
    {
        var log = new ClientDiagnosticLog(directory);
        log.Write(
            "UWF status result",
            "State=Unknown\nStandardOutput=Volume state: Un-protected");

        string contents = File.ReadAllText(log.LogPath);

        Assert(
            contents.Contains("UWF status result", StringComparison.Ordinal) &&
            contents.Contains("Volume state: Un-protected", StringComparison.Ordinal),
            "Client diagnostic log did not preserve the UWF command output.");
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}

static void TestUwfStatusColumnFormatting()
{
    var client = new ClientInfo();
    var changedProperties = new List<string?>();
    client.PropertyChanged += (_, args) =>
        changedProperties.Add(args.PropertyName);

    client.UwfState = UwfState.Locked;
    bool protectedStateIsDisplayed =
        client.UwfStatusText == "Protected" &&
        changedProperties.Contains("UwfStatusText");

    changedProperties.Clear();
    client.UwfState = UwfState.Unlocked;

    Assert(
        protectedStateIsDisplayed &&
        client.UwfStatusText == "Un-protected" &&
        changedProperties.Contains("UwfStatusText"),
        "The Host UWF column does not expose the current Volume state label.");
}

static void TestUwfStatusParserUsesCurrentDriveCVolumeState()
{
    const string capturedProtectedOutput = """
        Unified Write Filter Configuration Utility version 10.0.26200
        Copyright (C) Microsoft Corporation. All rights reserved.

        Current Session Settings

        FILTER SETTINGS
            Filter state:     ON

        VOLUME SETTINGS
        Volume 38c97866-e219-45c5-a3fc-6e2630a87435 [C:]
            Volume state:     Protected
            Volume ID:        38c97866-e219-45c5-a3fc-6e2630a87435

        Next Session Settings

        FILTER SETTINGS
            Filter state:     ON

        VOLUME SETTINGS
        Volume 38c97866-e219-45c5-a3fc-6e2630a87435 [C:]
            Volume state:     Un-protected
            Volume ID:        38c97866-e219-45c5-a3fc-6e2630a87435
        """;

    UwfStatusPayload enabledStatus = UwfManager.ParseStatus(
        capturedProtectedOutput);
    UwfStatusPayload filterOnlyEnabledStatus = UwfManager.ParseStatus(
        """
        Current Session Settings

        FILTER SETTINGS
            Filter state:     ON
            Commit pending:   N/A
            Shutdown pending: N/A
            HORM mode:        N/A
        """);

    const string disabledFilterOutput = """
        Current Session Settings

        FILTER SETTINGS
            Filter state:     OFF

        Next Session Settings

        FILTER SETTINGS
            Filter state:     ON
        """;
    const string enabledFilterUnprotectedVolumeOutput = """
        Current Session Settings

        FILTER SETTINGS
            Filter state:     ON

        VOLUME SETTINGS
        Volume 11111111-1111-1111-1111-111111111111 [D:]
            Volume state:     Protected

        Volume 00000000-0000-0000-0000-000000000000 [C:]
            Volume state:     Un-protected

        Next Session Settings

        FILTER SETTINGS
            Filter state:     ON

        VOLUME SETTINGS
        Volume 00000000-0000-0000-0000-000000000000 [C:]
            Volume state:     Protected
        """;

    UwfStatusPayload disabledStatus = UwfManager.ParseStatus(
        disabledFilterOutput);
    UwfStatusPayload unprotectedVolumeStatus = UwfManager.ParseStatus(
        enabledFilterUnprotectedVolumeOutput);
    UwfStatusPayload nextSessionOnlyStatus = UwfManager.ParseStatus(
        """
        Current Session Settings

        FILTER SETTINGS
            Filter state:     ON

        Next Session Settings

        VOLUME SETTINGS
        Volume 00000000-0000-0000-0000-000000000000 [C:]
            Volume state:     Protected
        """);

    Assert(
        enabledStatus.State == UwfState.Locked &&
        enabledStatus.FilterEnabled == true &&
        enabledStatus.DriveCProtected == true &&
        filterOnlyEnabledStatus.State == UwfState.Unknown &&
        filterOnlyEnabledStatus.FilterEnabled == true &&
        disabledStatus.State == UwfState.Unknown &&
        disabledStatus.FilterEnabled == false &&
        unprotectedVolumeStatus.State == UwfState.Unlocked &&
        unprotectedVolumeStatus.FilterEnabled == true &&
        unprotectedVolumeStatus.DriveCProtected == false &&
        nextSessionOnlyStatus.State == UwfState.Unknown &&
        nextSessionOnlyStatus.DriveCProtected is null,
        "UWF status parser did not derive On or Off from drive C in the " +
        "Current Session volume settings.");
}

static void TestUwfStatusParserHandlesConsoleControlCharacters()
{
    const string normalOutput = """
        Current Session Settings

        FILTER SETTINGS
            Filter state:     ON

        VOLUME SETTINGS
        Volume 38c97866-e219-45c5-a3fc-6e2630a87435 [C:]
            Volume state:     Un-protected

        Next Session Settings

        VOLUME SETTINGS
        Volume 38c97866-e219-45c5-a3fc-6e2630a87435 [C:]
            Volume state:     Protected
        """;

    var consoleOutput = new StringBuilder();
    foreach (char character in normalOutput)
    {
        consoleOutput.Append(character);
        consoleOutput.Append('\0');
    }

    UwfStatusPayload status = UwfManager.ParseStatus(
        consoleOutput.ToString());

    Assert(
        status.State == UwfState.Unlocked &&
        status.DriveCProtected == false,
        "UWF status parser did not tolerate console control characters " +
        "in the current Volume state output.");
}

static async Task TestUwfSimulationFixtureAsync()
{
    string fixturePath = Path.GetTempFileName();

    try
    {
        await File.WriteAllTextAsync(
            fixturePath,
            """
            Current Session Settings

            VOLUME SETTINGS
            Volume 38c97866-e219-45c5-a3fc-6e2630a87435 [C:]
                Volume state:     Un-protected

            Next Session Settings

            VOLUME SETTINGS
            Volume 38c97866-e219-45c5-a3fc-6e2630a87435 [C:]
                Volume state:     Protected
            """);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Uwf:SimulationFixturePath"] = fixturePath
            })
            .Build();
        var manager = new UwfManager(
            new TestHostEnvironment(Environments.Development),
            configuration,
            NullLogger<UwfManager>.Instance);

        UwfStatusPayload status = await manager.GetStatusAsync(
            CancellationToken.None);

        Assert(
            status.State == UwfState.Unlocked &&
            status.DriveCProtected == false &&
            status.Details.Contains("Simulated", StringComparison.Ordinal),
            "UWF simulation did not return the Current Session volume state.");

        await AssertThrowsAsync<InvalidOperationException>(
            manager.LockDriveCAsync(CancellationToken.None));

        var productionManager = new UwfManager(
            new TestHostEnvironment(Environments.Production),
            configuration,
            NullLogger<UwfManager>.Instance);

        await AssertThrowsAsync<InvalidOperationException>(
            productionManager.GetStatusAsync(CancellationToken.None));
    }
    finally
    {
        File.Delete(fixturePath);
    }
}

static void TestMainWindowStartup()
{
    Exception? failure = null;

    var thread = new Thread(() =>
    {
        try
        {
            var application = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            var window = new MainWindow();
            window.Show();

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                application.Shutdown();
            };
            timer.Start();
            application.Run();
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
        throw new InvalidOperationException(
            "MainWindow startup failed.",
            failure);
}

static void TestMainWindowLayout()
{
    Exception? failure = null;

    var thread = new Thread(() =>
    {
        try
        {
            var window = new MainWindow();
            DataGrid grid = (DataGrid)window.FindName("ClientGrid");

            Assert(
                window.Title == "Deep Fry v3.0.0",
                "Host title no longer preserves the Deep Fry identity.");
            Assert(
                window.FindName("RestartSelectedButton") is Button,
                "Host restart action is missing from the main window.");
            Assert(
                grid.RowHeaderWidth == 0,
                "DataGrid row header still creates a left-side gutter.");
            Assert(
                grid.Columns[4] is DataGridTextColumn uwfColumn &&
                uwfColumn.Binding is System.Windows.Data.Binding uwfBinding &&
                uwfBinding.Path.Path == "UwfStatusText",
                "The UWF column is not bound to the current Volume state text.");
            Assert(
                grid.Columns[5] is DataGridTemplateColumn lastResult &&
                lastResult.Width.UnitType == DataGridLengthUnitType.Star,
                "Last Result is not a responsive wrapping column.");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
        throw new InvalidOperationException(
            "MainWindow layout failed.",
            failure);
}

static void TestProtocolContract()
{
    RequestMessage request = RequestMessage.Create(
        MessageType.Register,
        new RegisterPayload
        {
            Hostname = "LAB-CLIENT-01",
            IpAddress = "10.22.4.13"
        },
        "request-1");

    string requestJson = JsonSerializer.Serialize(
        request,
        ProtocolJson.Options);
    RequestMessage roundTrippedRequest =
        JsonSerializer.Deserialize<RequestMessage>(
            requestJson,
            ProtocolJson.Options) ?? throw new InvalidOperationException(
                "Request could not be deserialized.");

    RegisterPayload? payload =
        roundTrippedRequest.GetPayload<RegisterPayload>();

    Assert(
        roundTrippedRequest.RequestId == "request-1" &&
        roundTrippedRequest.Type == MessageType.Register &&
        payload is not null &&
        payload.Hostname == "LAB-CLIENT-01" &&
        payload.IpAddress == "10.22.4.13",
        "Request protocol contract did not round-trip.");

    ResponseMessage response = ResponseMessage.CreateError(
        "request-1",
        new ErrorInfo
        {
            Code = "COMMAND_FAILED",
            Message = "Command execution failed."
        });

    ResponseMessage roundTrippedResponse =
        JsonSerializer.Deserialize<ResponseMessage>(
            JsonSerializer.Serialize(response, ProtocolJson.Options),
            ProtocolJson.Options) ?? throw new InvalidOperationException(
                "Response could not be deserialized.");

    Assert(
        !roundTrippedResponse.Success &&
        roundTrippedResponse.Type == MessageType.Response &&
        roundTrippedResponse.Error?.Code == "COMMAND_FAILED",
        "Response protocol contract did not round-trip.");
}

static async Task TestCommandDispatcherAsync()
{
    var systemPowerManager = new FakeSystemPowerManager();
    var dispatcher = new ClientCommandDispatcher(
        new FakeUwfManager(),
        systemPowerManager);

    ResponseMessage statusResponse = await dispatcher.DispatchAsync(
        RequestMessage.Create(
            MessageType.Command,
            new CommandRequestPayload { Name = "uwf.status" },
            "status-1"),
        CancellationToken.None);

    UwfStatusPayload? status =
        statusResponse.GetPayload<UwfStatusPayload>();

    Assert(
        statusResponse.Success && status?.State == UwfState.Locked,
        "UWF status command was not dispatched.");

    ResponseMessage restartResponse = await dispatcher.DispatchAsync(
        RequestMessage.Create(
            MessageType.Command,
            new CommandRequestPayload { Name = "system.restart" },
            "restart-1"),
        CancellationToken.None);

    Assert(
        restartResponse.Success &&
        systemPowerManager.RestartCallCount == 1 &&
        restartResponse.GetPayload<CommandResultPayload>()?.Details ==
            "Restart scheduled.",
        "System restart command was not dispatched through its safe manager.");

    ResponseMessage rejectedResponse = await dispatcher.DispatchAsync(
        RequestMessage.Create(
            MessageType.Command,
            new CommandRequestPayload { Name = "powershell arbitrary" },
            "reject-1"),
        CancellationToken.None);

    Assert(
        !rejectedResponse.Success &&
        rejectedResponse.Error?.Code == "COMMAND_NOT_ALLOWED",
        "Command allowlist did not reject an unknown command.");
}

static async Task TestHostDiscoversClientAsync()
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    using var sessionCancellation = new CancellationTokenSource(
        TimeSpan.FromSeconds(5));

    Task clientSessionTask = Task.Run(async () =>
    {
        TcpClient host = await listener.AcceptTcpClientAsync(
            sessionCancellation.Token);
        await ClientSession.RunAsync(
            host,
            new ClientCommandDispatcher(
                new FakeUwfManager(),
                new FakeSystemPowerManager()),
            sessionCancellation.Token);
    });

    var server = new HostServer(port);
    IReadOnlyList<ClientConnection> connections =
        await server.DiscoverClientsAsync(
            [IPAddress.Loopback.ToString()],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            sessionCancellation.Token);

    Assert(
        connections.Count == 1 &&
        connections[0].Hostname == Environment.MachineName &&
        connections[0].IpAddress == IPAddress.Loopback.ToString(),
        "Host did not discover the listening Client on the target IP.");

    ClientConnection connection = connections[0];
    Task listenTask = connection.ListenAsync(sessionCancellation.Token);
    ResponseMessage response = await connection.SendCommandAsync(
        "uwf.status",
        TimeSpan.FromSeconds(2),
        sessionCancellation.Token);

    Assert(
        response.Success &&
        response.GetPayload<UwfStatusPayload>()?.State == UwfState.Locked,
        "Discovered Client did not process a Host command.");

    sessionCancellation.Cancel();
    connection.Dispose();

    try
    {
        await Task.WhenAll(clientSessionTask, listenTask);
    }
    catch (Exception ex) when (
        ex is OperationCanceledException or IOException or ObjectDisposedException)
    {
    }
}

static async Task TestHeartbeatTimeoutAndDisconnectAsync()
{
    using var listener = StartListener();
    ConnectedClient client = await ConnectRegisteredClientAsync(
        listener,
        "LAB-CLIENT-01",
        "10.22.4.13");

    var registry = new ClientRegistry();
    client.Connection.HeartbeatReceived += (connection, heartbeat) =>
    {
        registry.RecordHeartbeat(connection, heartbeat, out _);
    };
    registry.Register(client.Connection);

    ClientInfo clientInfo = registry.Clients.Single();
    DateTime initialHeartbeat = clientInfo.LastHeartbeat!.Value;
    Task listenTask = client.Connection.ListenAsync(CancellationToken.None);

    await Task.Delay(50);
    await WriteLineAsync(client.Stream, "{\"Type\":\"heartbeat\"}");
    await WaitUntilAsync(
        () => clientInfo.LastHeartbeat > initialHeartbeat,
        "Host did not propagate the received heartbeat to the active client.");

    DateTime receivedHeartbeat = clientInfo.LastHeartbeat!.Value;
    Assert(
        registry.MarkStaleClients(
            receivedHeartbeat.AddSeconds(6.1),
            TimeSpan.FromSeconds(6)),
        "Client was not marked Offline after six seconds without a heartbeat.");
    Assert(clientInfo.Status == "Offline", "Stale client status is not Offline.");

    await WriteLineAsync(client.Stream, "{\"Type\":\"heartbeat\"}");
    await WaitUntilAsync(
        () => clientInfo.Status == "Online" &&
              clientInfo.LastHeartbeat > receivedHeartbeat,
        "Heartbeat from the active connection did not restore Online state.");

    client.TcpClient.Close();
    await listenTask;

    Assert(
        registry.Disconnect(client.Connection),
        "Active connection was not marked Offline after EOF.");
    Assert(clientInfo.Status == "Offline", "Disconnected client status is not Offline.");
    Assert(
        !registry.RecordHeartbeat(
            client.Connection,
            DateTime.Now,
            out _),
        "Heartbeat from a disconnected connection revived the client state.");

    client.Connection.Dispose();
}

static async Task TestDuplicateHostnameReconnectAsync()
{
    using var listener = StartListener();
    var registry = new ClientRegistry();

    ConnectedClient first = await ConnectRegisteredClientAsync(
        listener,
        "LAB-CLIENT-02",
        "10.22.4.14");
    first.Connection.HeartbeatReceived += (connection, heartbeat) =>
    {
        registry.RecordHeartbeat(connection, heartbeat, out _);
    };
    registry.Register(first.Connection);
    Task firstListenTask = first.Connection.ListenAsync(CancellationToken.None);
    DateTime firstInitialHeartbeat = first.Connection.LastHeartbeat;

    ConnectedClient second = await ConnectRegisteredClientAsync(
        listener,
        "lab-client-02",
        "10.22.4.24");
    second.Connection.HeartbeatReceived += (connection, heartbeat) =>
    {
        registry.RecordHeartbeat(connection, heartbeat, out _);
    };

    ClientConnection? replacedConnection = registry.Register(second.Connection);
    Task secondListenTask = second.Connection.ListenAsync(CancellationToken.None);

    Assert(
        ReferenceEquals(replacedConnection, first.Connection),
        "Duplicate hostname did not replace the previous active connection.");
    Assert(registry.Clients.Count == 1, "Duplicate hostname created a second row.");

    ClientInfo clientInfo = registry.Clients.Single();
    DateTime secondInitialHeartbeat = clientInfo.LastHeartbeat!.Value;
    Assert(
        ReferenceEquals(clientInfo.Connection, second.Connection),
        "Registry did not retain the newest connection as active.");
    Assert(clientInfo.IpAddress == "10.22.4.24", "Reconnect did not update IP address.");

    await Task.Delay(50);
    await WriteLineAsync(first.Stream, "{\"Type\":\"heartbeat\"}");
    await WaitUntilAsync(
        () => first.Connection.LastHeartbeat > firstInitialHeartbeat,
        "First connection did not receive its test heartbeat.");

    Assert(
        clientInfo.LastHeartbeat == secondInitialHeartbeat,
        "Heartbeat from an old connection modified the new active state.");

    await WriteLineAsync(second.Stream, "{\"Type\":\"heartbeat\"}");
    await WaitUntilAsync(
        () => clientInfo.LastHeartbeat > secondInitialHeartbeat,
        "Heartbeat from the new connection did not update active state.");

    first.TcpClient.Close();
    await firstListenTask;
    Assert(
        !registry.Disconnect(first.Connection),
        "Old disconnect marked the new active connection Offline.");

    second.TcpClient.Close();
    await secondListenTask;
    Assert(
        registry.Disconnect(second.Connection),
        "New active connection was not marked Offline after EOF.");

    first.Connection.Dispose();
    second.Connection.Dispose();
}

static async Task TestMalformedRegistrationAsync()
{
    using var listener = StartListener();
    Task<ClientConnection?> acceptTask = AcceptConnectionAsync(listener);

    using var client = new TcpClient();
    await client.ConnectAsync(
        IPAddress.Loopback,
        ((IPEndPoint)listener.LocalEndpoint).Port);

    await WriteLineAsync(client.GetStream(), "not-json");

    Assert(
        await acceptTask is null,
        "Malformed registration was accepted.");
}

static async Task TestMalformedAndFramedMessagesAsync()
{
    using var listener = StartListener();
    ConnectedClient client = await ConnectRegisteredClientAsync(
        listener,
        "LAB-CLIENT-03",
        "10.22.4.15");

    int heartbeatCount = 0;
    client.Connection.HeartbeatReceived += (_, _) => heartbeatCount++;
    Task listenTask = client.Connection.ListenAsync(CancellationToken.None);

    await WriteLineAsync(client.Stream, "not-json");
    await Task.Delay(50);

    Assert(
        !listenTask.IsCompleted,
        "Malformed message terminated the connection listener.");

    byte[] firstPart = Encoding.UTF8.GetBytes("{\"Type\":\"heart");
    byte[] remainingParts = Encoding.UTF8.GetBytes(
        "beat\"}\n{\"Type\":\"heartbeat\"}\n");

    await client.Stream.WriteAsync(firstPart);
    await Task.Delay(25);
    await client.Stream.WriteAsync(remainingParts);

    await WaitUntilAsync(
        () => heartbeatCount == 2,
        "Partial or consecutive newline-delimited messages were not handled.");

    client.TcpClient.Close();
    await listenTask;
    client.Connection.Dispose();
}

static async Task TestIncompleteMessageEofAsync()
{
    using var listener = StartListener();
    ConnectedClient client = await ConnectRegisteredClientAsync(
        listener,
        "LAB-CLIENT-04",
        "10.22.1.16");

    Task listenTask = client.Connection.ListenAsync(CancellationToken.None);
    byte[] incompleteMessage = Encoding.UTF8.GetBytes(
        "{\"type\":\"heartbeat\"");

    await client.Stream.WriteAsync(incompleteMessage);
    client.TcpClient.Close();
    await listenTask;
    client.Connection.Dispose();
}

static async Task TestMultipleRequestResponseAsync()
{
    using var listener = StartListener();
    ConnectedClient client = await ConnectRegisteredClientAsync(
        listener,
        "LAB-CLIENT-05",
        "10.22.1.17");
    using var reader = new JsonLineReader(client.Stream);
    using var writer = new JsonLineWriter(client.Stream);
    Task listenTask = client.Connection.ListenAsync(CancellationToken.None);

    Task<ResponseMessage> firstResponseTask =
        client.Connection.SendCommandAsync(
            "test.first",
            TimeSpan.FromSeconds(2),
            CancellationToken.None);
    Task<ResponseMessage> secondResponseTask =
        client.Connection.SendCommandAsync(
            "test.second",
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

    RequestMessage firstRequest = await ReadRequestAsync(reader);
    RequestMessage secondRequest = await ReadRequestAsync(reader);

    await writer.WriteAsync(
        ResponseMessage.CreateSuccess(secondRequest.RequestId, new { }),
        CancellationToken.None);
    await writer.WriteAsync(
        ResponseMessage.CreateSuccess(firstRequest.RequestId, new { }),
        CancellationToken.None);

    ResponseMessage[] responses = await Task.WhenAll(
        firstResponseTask,
        secondResponseTask);

    Assert(
        responses.All(x => x.Success) &&
        firstRequest.Type == MessageType.Command &&
        secondRequest.Type == MessageType.Command,
        "Multiple command requests were not correlated to their responses.");

    await writer.WriteAsync(
        ResponseMessage.CreateSuccess("unknown-request", new { }),
        CancellationToken.None);

    client.TcpClient.Close();
    await listenTask;
    client.Connection.Dispose();
}

static async Task TestRequestTimeoutAndDisconnectAsync()
{
    using var listener = StartListener();
    ConnectedClient client = await ConnectRegisteredClientAsync(
        listener,
        "LAB-CLIENT-06",
        "10.22.1.18");
    using var reader = new JsonLineReader(client.Stream);
    Task listenTask = client.Connection.ListenAsync(CancellationToken.None);

    Task<ResponseMessage> timeoutTask = client.Connection.SendCommandAsync(
        "test.timeout",
        TimeSpan.FromMilliseconds(100),
        CancellationToken.None);

    await ReadRequestAsync(reader);
    await AssertThrowsAsync<TimeoutException>(timeoutTask);

    using var cancellation = new CancellationTokenSource();
    Task<ResponseMessage> cancelledTask =
        client.Connection.SendCommandAsync(
            "test.cancelled",
            TimeSpan.FromSeconds(2),
            cancellation.Token);

    await ReadRequestAsync(reader);
    cancellation.Cancel();
    await AssertThrowsAsync<OperationCanceledException>(cancelledTask);

    Task<ResponseMessage> disconnectedTask =
        client.Connection.SendCommandAsync(
            "test.disconnected",
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

    await ReadRequestAsync(reader);
    client.TcpClient.Close();
    await listenTask;
    await AssertThrowsAsync<IOException>(disconnectedTask);
    client.Connection.Dispose();
}

static TcpListener StartListener()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    return listener;
}

static async Task<ConnectedClient> ConnectRegisteredClientAsync(
    TcpListener listener,
    string hostname,
    string ipAddress)
{
    Task<ClientConnection?> acceptTask = AcceptConnectionAsync(listener);
    var client = new TcpClient();
    await client.ConnectAsync(
        IPAddress.Loopback,
        ((IPEndPoint)listener.LocalEndpoint).Port);

    NetworkStream stream = client.GetStream();
    RequestMessage register = RequestMessage.Create(
        MessageType.Register,
        new RegisterPayload
        {
            Hostname = hostname,
            IpAddress = ipAddress
        });

    await WriteLineAsync(
        stream,
        JsonSerializer.Serialize(register, ProtocolJson.Options));

    ClientConnection connection =
        await acceptTask ?? throw new InvalidOperationException(
            "Registration was rejected.");

    return new ConnectedClient(client, stream, connection);
}

static async Task<ClientConnection?> AcceptConnectionAsync(TcpListener listener)
{
    TcpClient tcpClient = await listener.AcceptTcpClientAsync();
    return await ClientConnection.AcceptAsync(tcpClient, CancellationToken.None);
}

static async Task WriteLineAsync(NetworkStream stream, string value)
{
    byte[] data = Encoding.UTF8.GetBytes(value + "\n");
    await stream.WriteAsync(data);
}

static async Task WaitUntilAsync(
    Func<bool> predicate,
    string failureMessage)
{
    DateTime deadline = DateTime.UtcNow.AddSeconds(2);

    while (DateTime.UtcNow < deadline)
    {
        if (predicate())
            return;

        await Task.Delay(10);
    }

    throw new TimeoutException(failureMessage);
}

static async Task<RequestMessage> ReadRequestAsync(JsonLineReader reader)
{
    string line = await reader.ReadLineAsync(CancellationToken.None) ??
        throw new InvalidOperationException("Host did not send a request.");

    return JsonSerializer.Deserialize<RequestMessage>(
        line,
        ProtocolJson.Options) ?? throw new InvalidOperationException(
            "Host request could not be deserialized.");
}

static async Task AssertThrowsAsync<TException>(Task task)
    where TException : Exception
{
    try
    {
        await task;
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(
        $"Expected {typeof(TException).Name} was not thrown.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

file sealed record ConnectedClient(
    TcpClient TcpClient,
    NetworkStream Stream,
    ClientConnection Connection);

file sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "LifecycleTests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } =
        new NullFileProvider();
}

file sealed class FakeUwfManager : IUwfManager
{
    public Task<UwfStatusPayload> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new UwfStatusPayload
        {
            State = UwfState.Locked,
            FilterEnabled = true,
            DriveCProtected = true
        });
    }

    public Task<CommandResultPayload> LockDriveCAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new CommandResultPayload());
    }

    public Task<CommandResultPayload> UnlockDriveCAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new CommandResultPayload());
    }
}

file sealed class FakeSystemPowerManager : ISystemPowerManager
{
    public int RestartCallCount { get; private set; }

    public Task<CommandResultPayload> RestartAsync(
        CancellationToken cancellationToken)
    {
        RestartCallCount++;
        return Task.FromResult(new CommandResultPayload
        {
            Details = "Restart scheduled."
        });
    }
}
