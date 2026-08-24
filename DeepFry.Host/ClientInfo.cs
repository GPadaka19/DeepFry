using System.ComponentModel;
using System.Runtime.CompilerServices;
using DeepFry.Protocol;

namespace DeepFry.Host;

public sealed class ClientInfo : INotifyPropertyChanged
{
    private string _ipAddress = string.Empty;
    private string _status = string.Empty;
    private DateTime? _lastHeartbeat;
    private ClientConnection? _connection;
    private bool _isSelected;
    private UwfState _uwfState = UwfState.Unknown;
    private string _lastResult = string.Empty;

    public string Hostname { get; set; } = string.Empty;

    public string IpAddress
    {
        get => _ipAddress;
        set => SetField(ref _ipAddress, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public DateTime? LastHeartbeat
    {
        get => _lastHeartbeat;
        set => SetField(ref _lastHeartbeat, value);
    }

    public ClientConnection? Connection
    {
        get => _connection;
        set => SetField(ref _connection, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public UwfState UwfState
    {
        get => _uwfState;
        set
        {
            if (!SetField(ref _uwfState, value))
                return;

            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(UwfStatusText)));
        }
    }

    public string UwfStatusText => _uwfState switch
    {
        UwfState.Locked => "Protected",
        UwfState.Unlocked => "Un-protected",
        UwfState.Checking => "Checking...",
        _ => "Unknown"
    };

    public string LastResult
    {
        get => _lastResult;
        set => SetField(ref _lastResult, value);
    }

    public void Activate(ClientConnection connection)
    {
        IpAddress = connection.IpAddress;
        Status = "Online";
        LastHeartbeat = connection.LastHeartbeat;
        Connection = connection;
    }

    public bool RecordHeartbeat(
        ClientConnection connection,
        DateTime heartbeat)
    {
        if (!ReferenceEquals(Connection, connection))
            return false;

        LastHeartbeat = heartbeat;
        Status = "Online";
        return true;
    }

    public bool MarkOfflineIfStale(DateTime now, TimeSpan timeout)
    {
        if (Connection is null || LastHeartbeat is null || Status == "Offline")
            return false;

        if (now - LastHeartbeat.Value <= timeout)
            return false;

        Status = "Offline";
        return true;
    }

    public bool MarkDisconnected(ClientConnection connection)
    {
        if (!ReferenceEquals(Connection, connection))
            return false;

        Status = "Offline";
        Connection = null;
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
