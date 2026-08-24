namespace DeepFry.Host;

public sealed record HostConfiguration(string LabName)
{
    public static HostConfiguration Default { get; } =
        new("Lab belum dikonfigurasi");
}
