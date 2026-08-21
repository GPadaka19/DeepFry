using System.Diagnostics;
using System.Text.RegularExpressions;
using LabManagement.Protocol;

namespace LabManagement.Client;

public sealed class UwfManager : IUwfManager
{
    private const string DriveC = "C:";

    public async Task<UwfStatusPayload> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        UwfCommandResult filter = await RunAsync(
            ["get-config"],
            cancellationToken);
        UwfCommandResult volume = await RunAsync(
            ["volume", "get-config", DriveC],
            cancellationToken);

        string details = JoinOutput(filter, volume);

        if (filter.ExitCode != 0 || volume.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"uwfmgr get-config failed. {details}");
        }

        return ParseStatus(
            filter.StandardOutput,
            volume.StandardOutput,
            details);
    }

    internal static UwfStatusPayload ParseStatus(
        string filterOutput,
        string volumeOutput,
        string details = "")
    {
        bool? filterEnabled = FindBoolean(
            filterOutput,
            "filter\\s+(?:enabled|state)");
        bool? driveCProtected =
            FindDriveCProtection(volumeOutput) ??
            FindDriveCProtection(filterOutput) ??
            FindBoolean(
                volumeOutput,
                "(?:volume\\s+state|(?:volume\\s+)?protected)");

        UwfState state = (filterEnabled, driveCProtected) switch
        {
            (true, true) => UwfState.Locked,
            (false, _) or (_, false) => UwfState.Unlocked,
            _ => UwfState.Unknown
        };

        return new UwfStatusPayload
        {
            State = state,
            FilterEnabled = filterEnabled,
            DriveCProtected = driveCProtected,
            Details = details
        };
    }

    public async Task<CommandResultPayload> LockDriveCAsync(
        CancellationToken cancellationToken)
    {
        UwfCommandResult protect = await RunAsync(
            ["volume", "protect", DriveC],
            cancellationToken);
        UwfCommandResult enable = await RunAsync(
            ["filter", "enable"],
            cancellationToken);

        string details = JoinOutput(protect, enable);

        if (protect.ExitCode != 0 || enable.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Unable to lock drive C:. {details}");
        }

        return new CommandResultPayload
        {
            RestartRequired = true,
            Details = "Drive C: will be protected after restart."
        };
    }

    public async Task<CommandResultPayload> UnlockDriveCAsync(
        CancellationToken cancellationToken)
    {
        UwfCommandResult unprotect = await RunAsync(
            ["volume", "unprotect", DriveC],
            cancellationToken);

        if (unprotect.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Unable to unlock drive C:. {JoinOutput(unprotect)}");
        }

        return new CommandResultPayload
        {
            RestartRequired = true,
            Details = "Drive C: will be unprotected after restart."
        };
    }

    private static async Task<UwfCommandResult> RunAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        string executablePath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.System),
            "uwfmgr.exe");

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Unable to start uwfmgr.exe.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(
            cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(
            cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new UwfCommandResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static bool? FindBoolean(string text, string label)
    {
        Match match = Regex.Match(
            text,
            $"{label}\\s*:\\s*(?<value>" +
            "yes|no|on|off|true|false|enabled|disabled|" +
            "protected|unprotected)",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        return ParseBooleanValue(match.Groups["value"].Value);
    }

    private static bool? FindDriveCProtection(string text)
    {
        Match match = Regex.Match(
            text,
            "^\\s*volume[^\\r\\n]*\\[c:\\][^\\r\\n]*\\r?\\n" +
            "\\s*volume\\s+state\\s*:\\s*" +
            "(?<value>protected|unprotected)\\b",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        return match.Success
            ? ParseBooleanValue(match.Groups["value"].Value)
            : null;
    }

    private static bool? ParseBooleanValue(string value) =>
        value.ToLowerInvariant() switch
        {
            "yes" or "on" or "true" or "enabled" or "protected" => true,
            "no" or "off" or "false" or "disabled" or "unprotected" => false,
            _ => null
        };

    private static string JoinOutput(params UwfCommandResult[] results)
    {
        return string.Join(
            Environment.NewLine,
            results.SelectMany(result => new[]
            {
                result.StandardOutput,
                result.StandardError
            }).Where(text => !string.IsNullOrWhiteSpace(text))).Trim();
    }

    private sealed record UwfCommandResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
