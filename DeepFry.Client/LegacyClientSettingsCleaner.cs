namespace DeepFry.Client;

internal static class LegacyClientSettingsCleaner
{
    public static void RemovePairingKeyFile(ILogger logger)
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "LabManagement",
            "Client",
            "client-settings.json");

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                logger.LogInformation(
                    "Konfigurasi pairing key versi lama telah dihapus.");
            }
        }
        catch (IOException ex)
        {
            logger.LogWarning(
                ex,
                "Konfigurasi pairing key lama tidak dapat dihapus dari {path}.",
                path);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(
                ex,
                "Tidak memiliki izin untuk menghapus pairing key lama dari {path}.",
                path);
        }
    }
}
