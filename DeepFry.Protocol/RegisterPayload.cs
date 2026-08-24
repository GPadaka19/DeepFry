namespace DeepFry.Protocol;

public sealed class RegisterPayload
{
    public string Hostname { get; init; } = string.Empty;

    public string IpAddress { get; init; } = string.Empty;
}
