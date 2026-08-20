namespace LabManagement.Protocol;

public sealed class CommandResultPayload
{
    public bool RestartRequired { get; init; }

    public string Details { get; init; } = string.Empty;
}
