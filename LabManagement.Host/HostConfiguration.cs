namespace LabManagement.Host;

public sealed record HostConfiguration(string LabName, int TcpPort)
{
    public static HostConfiguration Default { get; } =
        new("Lab belum dikonfigurasi", 5020);
}
