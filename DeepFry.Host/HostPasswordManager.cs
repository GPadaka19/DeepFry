using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace DeepFry.Host;

public enum PasswordConfigurationStatus
{
    NotConfigured,
    Ready,
    Invalid
}

public sealed class HostPasswordManager
{
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const int Iterations = 210_000;
    private const string SettingsFileName = "host-settings.json";
    private readonly string _settingsPath;

    public HostPasswordManager(string? settingsDirectory = null)
    {
        string directory = settingsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "DeepFry");
        _settingsPath = Path.Combine(directory, SettingsFileName);
    }

    public PasswordConfigurationStatus Status => ReadSettings() switch
    {
        null when !File.Exists(_settingsPath) => PasswordConfigurationStatus.NotConfigured,
        null => PasswordConfigurationStatus.Invalid,
        _ => PasswordConfigurationStatus.Ready
    };

    public void SetPassword(string password)
    {
        ValidateNewPassword(password);
        HostSettings? existingSettings = ReadSettings();
        byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);
        byte[] hash = HashPassword(password, salt, Iterations);
        var settings = new HostSettings
        {
            PasswordHash = Convert.ToBase64String(hash),
            PasswordSalt = Convert.ToBase64String(salt),
            PasswordIterations = Iterations,
            LabName = existingSettings?.LabName ?? HostConfiguration.Default.LabName
        };

        WriteSettings(settings);
    }

    public HostConfiguration GetConfiguration()
    {
        HostSettings? settings = ReadSettings() ?? throw new InvalidOperationException(
            "Password Host belum dikonfigurasi.");
        var configuration = new HostConfiguration(
            string.IsNullOrWhiteSpace(settings.LabName)
                ? HostConfiguration.Default.LabName
                : settings.LabName);

        if (ContainsLegacyNetworkSettings())
        {
            WriteSettings(new HostSettings
            {
                PasswordHash = settings.PasswordHash,
                PasswordSalt = settings.PasswordSalt,
                PasswordIterations = settings.PasswordIterations,
                LabName = configuration.LabName
            });
        }

        return configuration;
    }

    public void SaveConfiguration(HostConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.LabName))
        {
            throw new ArgumentException("Konfigurasi Host tidak valid.");
        }

        HostSettings settings = ReadSettings() ?? throw new InvalidOperationException(
            "Password Host belum dikonfigurasi.");
        WriteSettings(new HostSettings
        {
            PasswordHash = settings.PasswordHash,
            PasswordSalt = settings.PasswordSalt,
            PasswordIterations = settings.PasswordIterations,
            LabName = configuration.LabName.Trim()
        });
    }

    private void WriteSettings(HostSettings settings)
    {

        string directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = _settingsPath + ".tmp";
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _settingsPath, overwrite: true);
    }

    public bool VerifyPassword(string password)
    {
        HostSettings? settings = ReadSettings();
        if (settings is null || string.IsNullOrEmpty(password))
            return false;

        try
        {
            byte[] salt = Convert.FromBase64String(settings.PasswordSalt);
            byte[] expectedHash = Convert.FromBase64String(settings.PasswordHash);
            byte[] actualHash = HashPassword(password, salt, settings.PasswordIterations);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public bool ChangePassword(string currentPassword, string newPassword)
    {
        if (!VerifyPassword(currentPassword))
            return false;

        SetPassword(newPassword);
        return true;
    }

    private static byte[] HashPassword(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, HashAlgorithmName.SHA256, HashLength);

    private static void ValidateNewPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new ArgumentException(
                "Password harus memiliki minimal 6 karakter.",
                nameof(password));
        }
    }

    private HostSettings? ReadSettings()
    {
        if (!File.Exists(_settingsPath))
            return null;

        try
        {
            HostSettings? settings = JsonSerializer.Deserialize<HostSettings>(File.ReadAllText(_settingsPath));
            if (settings is null ||
                settings.PasswordIterations < 100_000 ||
                string.IsNullOrWhiteSpace(settings.PasswordHash) ||
                string.IsNullOrWhiteSpace(settings.PasswordSalt))
            {
                return null;
            }

            return settings;
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

    private bool ContainsLegacyNetworkSettings()
    {
        try
        {
            string json = File.ReadAllText(_settingsPath);
            return json.Contains(
                       "\"ClientSharedSecret\"",
                       StringComparison.OrdinalIgnoreCase) ||
                   json.Contains(
                       "\"TcpPort\"",
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private sealed class HostSettings
    {
        public string PasswordHash { get; init; } = string.Empty;
        public string PasswordSalt { get; init; } = string.Empty;
        public int PasswordIterations { get; init; }
        public string LabName { get; init; } = HostConfiguration.Default.LabName;
    }
}
