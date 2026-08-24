using System.Diagnostics;
using DeepFry.Protocol;

namespace DeepFry.Client;

public sealed class SystemPowerManager : ISystemPowerManager
{
    private const int RestartDelaySeconds = 5;

    public async Task<CommandResultPayload> RestartAsync(
        CancellationToken cancellationToken)
    {
        string executablePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "shutdown.exe");

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string argument in new[]
        {
            "/r",
            "/t",
            RestartDelaySeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            "/d",
            "p:0:0",
            "/c",
            "Restart requested by Deep Fry - 22.11.5020"
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException(
                "Unable to start shutdown.exe.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(
            cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(
            cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        string output = string.Join(
            Environment.NewLine,
            new[] { await standardOutput, await standardError }
                .Where(text => !string.IsNullOrWhiteSpace(text)))
            .Trim();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(output)
                    ? $"shutdown.exe failed with exit code {process.ExitCode}."
                    : output);
        }

        return new CommandResultPayload
        {
            Details = $"Restart scheduled in {RestartDelaySeconds} seconds."
        };
    }
}
