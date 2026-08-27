namespace DeepFry.Protocol;

public sealed class UwfStatusPayload
{
    public UwfState State { get; init; } = UwfState.Unknown;

    public UwfState NextSessionState { get; init; } = UwfState.Unknown;

    public bool? FilterEnabled { get; init; }

    public bool? FilterEnabledNextSession { get; init; }

    public bool? DriveCProtected { get; init; }

    public bool RestartRequired { get; init; }

    public string Details { get; init; } = string.Empty;
}
