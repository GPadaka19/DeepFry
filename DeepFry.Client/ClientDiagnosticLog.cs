using System.IO;
using System.Text;

namespace DeepFry.Client;

internal sealed class ClientDiagnosticLog
{
    private const long MaximumLogBytes = 5 * 1024 * 1024;
    private static readonly object WriteLock = new();
    private readonly string _logPath;

    public ClientDiagnosticLog(string? directory = null)
    {
        string logDirectory = directory ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "DeepFry",
            "logs");
        _logPath = Path.Combine(logDirectory, "client.log");
    }

    internal string LogPath => _logPath;

    public void Write(string title, string details)
    {
        try
        {
            lock (WriteLock)
            {
                string directory = Path.GetDirectoryName(_logPath)!;
                Directory.CreateDirectory(directory);
                RotateIfNeeded();

                string entry = $"[{DateTimeOffset.Now:O}] {title}" +
                    Environment.NewLine +
                    details.Trim() +
                    Environment.NewLine +
                    new string('-', 80) +
                    Environment.NewLine;

                File.AppendAllText(_logPath, entry, Encoding.UTF8);
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
        var logFile = new FileInfo(_logPath);

        if (!logFile.Exists || logFile.Length < MaximumLogBytes)
            return;

        File.Move(_logPath, _logPath + ".previous", overwrite: true);
    }
}
