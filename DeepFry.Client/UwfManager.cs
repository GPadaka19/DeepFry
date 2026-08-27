using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DeepFry.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeepFry.Client;

public sealed class UwfManager : IUwfManager
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UwfManager> _logger;
    private readonly ClientDiagnosticLog _diagnosticLog = new();

    public UwfManager(
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<UwfManager> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<UwfStatusPayload> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        string? simulationFixturePath = GetSimulationFixturePath();

        if (simulationFixturePath is not null)
        {
            if (!File.Exists(simulationFixturePath))
            {
                throw new InvalidOperationException(
                    $"UWF simulation fixture was not found: " +
                    simulationFixturePath);
            }

            string simulatedOutput = await File.ReadAllTextAsync(
                simulationFixturePath,
                cancellationToken);

            UwfStatusPayload simulatedStatus = ParseStatus(
                simulatedOutput,
                $"Simulated UWF configuration from {simulationFixturePath}");
            LogStatusResult(
                "simulation fixture",
                0,
                simulatedOutput,
                string.Empty,
                simulatedStatus);
            return simulatedStatus;
        }

        UwfCommandResult configuration;

        try
        {
            configuration = await RunAsync(
                ["get-config"],
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogStatusFailure("uwfmgr.exe could not be started", ex);
            throw;
        }

        string details = JoinOutput(configuration);

        if (configuration.ExitCode != 0)
        {
            LogStatusResult(
                "uwfmgr.exe get-config",
                configuration.ExitCode,
                configuration.StandardOutput,
                configuration.StandardError,
                null);
            throw new InvalidOperationException(
                $"uwfmgr get-config failed. {details}");
        }

        UwfStatusPayload status = ParseStatus(details, details);
        LogStatusResult(
            "uwfmgr.exe get-config",
            configuration.ExitCode,
            configuration.StandardOutput,
            configuration.StandardError,
            status);
        return status;
    }

    internal static UwfStatusPayload ParseStatus(
        string output,
        string details = "")
    {
        string normalizedOutput = NormalizeConsoleOutput(output);
        string currentSession = ExtractCurrentSession(normalizedOutput);
        string nextSession = ExtractNextSession(normalizedOutput);
        bool? filterEnabled = FindBoolean(
            currentSession,
            "filter\\s+state");
        bool? nextFilterEnabled = FindBoolean(
            nextSession,
            "filter\\s+state");

        UwfState state = ToState(filterEnabled);
        UwfState nextState = ToState(nextFilterEnabled);

        return new UwfStatusPayload
        {
            State = state,
            NextSessionState = nextState,
            FilterEnabled = filterEnabled,
            FilterEnabledNextSession = nextFilterEnabled,
            Details = details
        };
    }

    private static UwfState ToState(bool? protectedValue) =>
        protectedValue switch
        {
            true => UwfState.Locked,
            false => UwfState.Unlocked,
            _ => UwfState.Unknown
        };

    public async Task<CommandResultPayload> LockAsync(
        CancellationToken cancellationToken)
    {
        EnsureUwfControlIsAvailable();

        UwfCommandResult enable = await RunAsync(
            ["filter", "enable"],
            cancellationToken);

        string details = JoinOutput(enable);

        if (enable.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Unable to enable the UWF filter. {details}");
        }

        return new CommandResultPayload
        {
            RestartRequired = true,
            Details = "UWF filter will be enabled after restart."
        };
    }

    public async Task<CommandResultPayload> UnlockAsync(
        CancellationToken cancellationToken)
    {
        EnsureUwfControlIsAvailable();

        UwfCommandResult disable = await RunAsync(
            ["filter", "disable"],
            cancellationToken);

        if (disable.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Unable to disable the UWF filter. {JoinOutput(disable)}");
        }

        return new CommandResultPayload
        {
            RestartRequired = true,
            Details = "UWF filter will be disabled after restart."
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

    private static string ExtractCurrentSession(string output)
    {
        const string currentHeader = "Current Session Settings";
        const string nextHeader = "Next Session Settings";

        int currentStart = output.IndexOf(
            currentHeader,
            StringComparison.OrdinalIgnoreCase);

        if (currentStart < 0)
            return string.Empty;

        currentStart += currentHeader.Length;
        int nextStart = output.IndexOf(
            nextHeader,
            currentStart,
            StringComparison.OrdinalIgnoreCase);

        return nextStart < 0
            ? output[currentStart..]
            : output[currentStart..nextStart];
    }

    private static string ExtractNextSession(string output)
    {
        const string nextHeader = "Next Session Settings";

        int nextStart = output.IndexOf(
            nextHeader,
            StringComparison.OrdinalIgnoreCase);

        if (nextStart < 0)
            return string.Empty;

        nextStart += nextHeader.Length;
        return output[nextStart..];
    }

    private static string NormalizeConsoleOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
            return string.Empty;

        var normalized = new StringBuilder(output.Length);

        foreach (char character in output)
        {
            if (character == '\0')
                continue;

            if (char.IsControl(character) &&
                character is not '\r' and not '\n' and not '\t')
            {
                continue;
            }

            normalized.Append(character);
        }

        return normalized.ToString();
    }

    private string? GetSimulationFixturePath()
    {
        string? configuredPath =
            _configuration["Uwf:SimulationFixturePath"];

        if (string.IsNullOrWhiteSpace(configuredPath))
            return null;

        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "UWF simulation is only available in the Development " +
                "environment.");
        }

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_environment.ContentRootPath, configuredPath);
    }

    private void EnsureUwfControlIsAvailable()
    {
        if (GetSimulationFixturePath() is not null)
        {
            throw new InvalidOperationException(
                "UWF lock and unlock are disabled while simulation is active.");
        }
    }

    private void LogStatusResult(
        string source,
        int exitCode,
        string standardOutput,
        string standardError,
        UwfStatusPayload? status)
    {
        string parsedStatus = status is null
            ? "No status payload was produced."
            : $"State={status.State}; NextSessionState={status.NextSessionState}; " +
              $"FilterEnabled={status.FilterEnabled}; " +
              $"FilterEnabledNextSession={status.FilterEnabledNextSession}";
        string details = $"Source={source}{Environment.NewLine}" +
            $"ExitCode={exitCode}{Environment.NewLine}" +
            $"{parsedStatus}{Environment.NewLine}" +
            $"StandardOutput:{Environment.NewLine}{standardOutput}{Environment.NewLine}" +
            $"StandardError:{Environment.NewLine}{standardError}";

        _diagnosticLog.Write("UWF status result", details);

        if (status?.State == UwfState.Unknown || status is null)
            _logger.LogWarning("UWF status was not resolved. {details}", details);
        else
            _logger.LogInformation("UWF status resolved. {details}", details);
    }

    private void LogStatusFailure(string title, Exception exception)
    {
        string details = $"{exception.GetType().Name}: {exception.Message}";
        _diagnosticLog.Write(title, details);
        _logger.LogError(exception, "{title}", title);
    }

    private static bool? ParseBooleanValue(string value) =>
        value.ToLowerInvariant() switch
        {
            "yes" or "on" or "true" or "enabled" or "protected" => true,
            "no" or "off" or "false" or "disabled" or
                "unprotected" or "un-protected" => false,
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
