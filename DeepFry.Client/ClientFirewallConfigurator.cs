using System.Diagnostics;

namespace DeepFry.Client;

internal static class ClientFirewallConfigurator
{
    private const string RuleName = "Deep Fry Client TCP 5020";

    public static async Task EnsureInboundRuleAsync(
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            int showExitCode = await RunNetshAsync(
                ["advfirewall", "firewall", "show", "rule", $"name={RuleName}"],
                cancellationToken);

            if (showExitCode == 0)
                return;

            int addExitCode = await RunNetshAsync(
                [
                    "advfirewall", "firewall", "add", "rule",
                    $"name={RuleName}",
                    "dir=in", "action=allow", "protocol=TCP",
                    $"localport={Worker.ListenPort}",
                    "remoteip=localsubnet", "profile=any"
                ],
                cancellationToken);

            if (addExitCode != 0)
            {
                logger.LogWarning(
                    "Firewall rule TCP {port} tidak dapat dibuat otomatis. " +
                    "Izinkan DeepFry.Client pada Windows Firewall.",
                    Worker.ListenPort);
            }
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                ex,
                "Pemeriksaan Windows Firewall gagal. " +
                "Koneksi Host mungkin perlu diizinkan secara manual.");
        }
    }

    private static async Task<int> RunNetshAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "netsh.exe"))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Unable to start netsh.exe.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(
            cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(
            cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(standardOutput, standardError);
        return process.ExitCode;
    }
}
