using System.Windows;
using System.Windows.Input;
using LabManagement.Protocol;

namespace LabManagement.Host
{
    public partial class MainWindow : Window
    {
        private readonly HostPasswordManager? _passwordManager;
        private HostConfiguration _configuration = HostConfiguration.Default;

        private readonly ClientRegistry _clients = new();

        private HostServer? _server;
        private CancellationTokenSource? _cancellationTokenSource;

        public MainWindow(HostPasswordManager? passwordManager = null)
        {
            InitializeComponent();
            _passwordManager = passwordManager;

            ClientGrid.ItemsSource = _clients.Clients;

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            _cancellationTokenSource =
                new CancellationTokenSource();

            _configuration = _passwordManager?.GetConfiguration() ??
                HostConfiguration.Default;
            _server = new HostServer();

            StatusText.Text =
                $"{_configuration.LabName} | Scanning TCP 5020";

            _ = DiscoverClientsAsync(
                _cancellationTokenSource.Token);

            _ = MonitorClientsAsync(
                _cancellationTokenSource.Token);
        }

        private async Task DiscoverClientsAsync(
            CancellationToken cancellationToken)
        {
            if (_server is null)
                return;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    IReadOnlySet<string> connectedAddresses =
                        await Dispatcher.InvokeAsync(
                            _clients.GetConnectedIpAddresses);
                    IReadOnlyList<ClientConnection> connections =
                        await _server.DiscoverClientsAsync(
                            LabNetworkDiscovery.GetCandidateClientAddresses(),
                            connectedAddresses,
                            cancellationToken);

                    await Dispatcher.InvokeAsync(() =>
                    {
                        foreach (ClientConnection connection in connections)
                        {
                            AddOrUpdateClient(connection);
                            _ = MonitorClientConnectionAsync(
                                connection,
                                cancellationToken);
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        StatusText.Text =
                            $"Discovery error: {ex.Message}";
                    });
                }

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(3),
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task MonitorClientConnectionAsync(
            ClientConnection connection,
            CancellationToken cancellationToken)
        {
            try
            {
                await connection.ListenAsync(
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }

            connection.HeartbeatReceived -=
                Connection_HeartbeatReceived;

            await Dispatcher.InvokeAsync(() =>
            {
                if (_clients.Disconnect(connection))
                {
                    ClientGrid.Items.Refresh();
                    UpdateStatusText();
                }
            });

            connection.Dispose();
        }

        private async Task MonitorClientsAsync(
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(1),
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (_clients.MarkStaleClients(
                            DateTime.Now,
                            TimeSpan.FromSeconds(6)))
                    {
                        ClientGrid.Items.Refresh();
                        UpdateStatusText();
                    }
                });
            }
        }

        private void AddOrUpdateClient(
            ClientConnection connection)
        {
            connection.HeartbeatReceived +=
                Connection_HeartbeatReceived;

            ClientConnection? previousConnection =
                _clients.Register(connection);

            ClientGrid.Items.Refresh();

            if (previousConnection is not null &&
                !ReferenceEquals(previousConnection, connection))
            {
                previousConnection.HeartbeatReceived -=
                    Connection_HeartbeatReceived;

                previousConnection.Dispose();
            }

            UpdateStatusText();
        }

        private void Connection_HeartbeatReceived(
            ClientConnection connection,
            DateTime heartbeat)
        {
            if (Dispatcher.HasShutdownStarted ||
                Dispatcher.HasShutdownFinished)
            {
                return;
            }

            if (Dispatcher.CheckAccess())
            {
                ApplyHeartbeat(connection, heartbeat);
                return;
            }

            _ = Dispatcher.InvokeAsync(() =>
                ApplyHeartbeat(connection, heartbeat));
        }

        private void ApplyHeartbeat(
            ClientConnection connection,
            DateTime heartbeat)
        {
            if (!_clients.RecordHeartbeat(
                    connection,
                    heartbeat,
                    out bool becameOnline))
            {
                return;
            }

            if (becameOnline)
            {
                ClientGrid.Items.Refresh();
                UpdateStatusText();
            }
        }

        private void UpdateStatusText()
        {
            StatusText.Text =
                $"{_configuration.LabName} | TCP 5020 | " +
                $"Connected: {_clients.OnlineCount} / {_clients.Clients.Count}";
        }

        private async void RefreshUwfStatus_Click(
            object sender,
            RoutedEventArgs e)
        {
            await ExecuteCommandAsync(
                "uwf.status",
                _clients.Clients.Where(
                    client => client.Status == "Online"));
        }

        private async void LockSelected_Click(
            object sender,
            RoutedEventArgs e)
        {
            await ExecuteProtectedCommandAsync("uwf.lock", "lock");
        }

        private async void UnlockSelected_Click(
            object sender,
            RoutedEventArgs e)
        {
            await ExecuteProtectedCommandAsync("uwf.unlock", "unlock");
        }

        private async Task ExecuteProtectedCommandAsync(
            string commandName,
            string actionName)
        {
            List<ClientInfo> targets = _clients.Clients
                .Where(client =>
                    client.IsSelected &&
                    client.Status == "Online")
                .ToList();

            if (targets.Count == 0)
            {
                MessageBox.Show(
                    "Pilih minimal satu PC yang sedang Online.",
                    "LabManagement",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                $"{actionName} drive C: pada {targets.Count} PC? " +
                "Perubahan akan berlaku setelah restart.",
                "Konfirmasi tindakan UWF",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            await ExecuteCommandAsync(commandName, targets);
        }

        private async Task ExecuteCommandAsync(
            string commandName,
            IEnumerable<ClientInfo> targets)
        {
            ClientInfo[] targetArray = targets.ToArray();

            foreach (ClientInfo client in targetArray)
            {
                client.LastResult = "Sending command...";

                if (commandName == "uwf.status")
                    client.UwfState = UwfState.Checking;
            }

            await Task.WhenAll(targetArray.Select(client =>
                SendCommandAsync(client, commandName)));
        }

        private static async Task SendCommandAsync(
            ClientInfo client,
            string commandName)
        {
            ClientConnection? connection = client.Connection;

            if (connection is null || client.Status != "Online")
            {
                client.LastResult = "Offline";
                return;
            }

            try
            {
                ResponseMessage response = await connection.SendCommandAsync(
                    commandName,
                    TimeSpan.FromSeconds(10),
                    CancellationToken.None);

                if (!response.Success)
                {
                    client.LastResult = response.Error?.Message ??
                        "Command failed.";

                    if (commandName == "uwf.status")
                        client.UwfState = UwfState.Unknown;

                    return;
                }

                if (commandName == "uwf.status")
                {
                    UwfStatusPayload? status =
                        response.GetPayload<UwfStatusPayload>();

                    if (status is null)
                    {
                        client.UwfState = UwfState.Unknown;
                        client.LastResult = "Invalid UWF status response.";
                        return;
                    }

                    client.UwfState = status.State;
                    client.LastResult = status.Details;
                    return;
                }

                CommandResultPayload? result =
                    response.GetPayload<CommandResultPayload>();

                client.LastResult = result?.Details ?? "Completed.";
                client.UwfState = UwfState.Checking;
            }
            catch (TimeoutException)
            {
                client.LastResult = "Command timed out.";

                if (commandName == "uwf.status")
                    client.UwfState = UwfState.Unknown;
            }
            catch (Exception ex)
            {
                client.LastResult = ex.Message;

                if (commandName == "uwf.status")
                    client.UwfState = UwfState.Unknown;
            }
        }

        private void Settings_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog = new PasswordDialog(PasswordDialogMode.Change)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            if (App.PasswordManager.ChangePassword(
                    dialog.CurrentPassword!,
                    dialog.NewPassword!))
            {
                MessageBox.Show(
                    "Password Host berhasil diganti.",
                    "Deep Fry",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBox.Show(
                "Password saat ini tidak sesuai.",
                "Deep Fry",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void LabSettings_Click(object sender, RoutedEventArgs e)
        {
            HostPasswordManager manager = _passwordManager ?? App.PasswordManager;
            var dialog = new HostConfigurationDialog(manager.GetConfiguration()) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.Configuration is null)
                return;

            manager.SaveConfiguration(dialog.Configuration);
            _configuration = dialog.Configuration;
            UpdateStatusText();
            MessageBox.Show(
                "Nama lab berhasil disimpan.",
                "Deep Fry", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SelectAllCheckBox_Checked(
            object sender,
            RoutedEventArgs e)
        {
            SetSelectionForAll(true);
        }

        private void SelectAllCheckBox_Unchecked(
            object sender,
            RoutedEventArgs e)
        {
            SetSelectionForAll(false);
        }

        private void SetSelectionForAll(bool isSelected)
        {
            foreach (ClientInfo client in _clients.Clients)
                client.IsSelected = isSelected;
        }

        private void Window_PreviewMouseDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (!ClientGrid.IsMouseOver)
                ClientGrid.UnselectAll();
        }

        private void MainWindow_Closed(
            object? sender,
            EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
        }
    }
}
