using System.IO;
using System.Text;

namespace LabManagement.Host;

public sealed class HostDiagnosticLog
{
    private const long MaximumLogBytes = 5 * 1024 * 1024;
    private static readonly object WriteLock = new();

    public HostDiagnosticLog(string? directory = null)
    {
        string logDirectory = directory ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "LabManagement",
            "logs");
        LogPath = Path.Combine(logDirectory, "host.log");
    }

    public string LogPath { get; }

    public void Write(string title, string details)
    {
        try
        {
            lock (WriteLock)
            {
                string directory = Path.GetDirectoryName(LogPath)!;
                Directory.CreateDirectory(directory);
                RotateIfNeeded();

                string entry = $"[{DateTimeOffset.Now:O}] {title}" +
                    Environment.NewLine +
                    details.Trim() +
                    Environment.NewLine +
                    new string('-', 80) +
                    Environment.NewLine;

                File.AppendAllText(LogPath, entry, Encoding.UTF8);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void RotateIfNeeded()
    {
        var logFile = new FileInfo(LogPath);

        if (!logFile.Exists || logFile.Length < MaximumLogBytes)
            return;

        File.Move(LogPath, LogPath + ".previous", overwrite: true);
    }
}
