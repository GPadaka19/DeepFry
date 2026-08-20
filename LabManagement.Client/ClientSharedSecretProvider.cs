using System.Text.Json;

namespace LabManagement.Client;

public sealed class ClientSharedSecretProvider
{
    private const string EnvironmentVariableName = "LABMANAGEMENT_SHARED_SECRET";

    public string? GetSharedSecret()
    {
        string? environmentSecret = GetEnvironmentValue(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(environmentSecret))
            return environmentSecret;

        string? developmentSecret = ReadEnvironmentFile(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(developmentSecret))
            return developmentSecret;

        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "LabManagement",
            "Client",
            "client-settings.json");

        try
        {
            var settings = JsonSerializer.Deserialize<ClientSettings>(
                File.ReadAllText(path));
            return settings?.SharedSecret;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string? GetHostIpOverride() =>
        GetEnvironmentValue("LABMANAGEMENT_HOST_IP") ??
        ReadEnvironmentFile("LABMANAGEMENT_HOST_IP");

    private static string? GetEnvironmentValue(string name) =>
        Environment.GetEnvironmentVariable(name);

    private static string? ReadEnvironmentFile(string variableName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, ".env");

        try
        {
            foreach (string line in File.ReadLines(path))
            {
                string prefix = variableName + "=";
                string trimmedLine = line.Trim();

                if (trimmedLine.StartsWith(prefix, StringComparison.Ordinal))
                    return trimmedLine[prefix.Length..].Trim();
            }
        }
        catch (IOException)
        {
        }

        return null;
    }

    private sealed class ClientSettings
    {
        public string? SharedSecret { get; init; }
    }
}
