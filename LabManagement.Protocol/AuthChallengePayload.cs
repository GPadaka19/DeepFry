namespace LabManagement.Protocol;

public sealed class AuthChallengePayload
{
    public string Challenge { get; init; } = string.Empty;
}
