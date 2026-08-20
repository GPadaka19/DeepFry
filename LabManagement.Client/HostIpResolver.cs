using System.Net;

namespace LabManagement.Client;

public static class HostIpResolver
{
    public static string Resolve(string localIp, string? overrideIp = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideIp))
        {
            if (!IPAddress.TryParse(overrideIp, out IPAddress? parsedOverride) ||
                parsedOverride.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                throw new InvalidOperationException(
                    $"LABMANAGEMENT_HOST_IP tidak valid: {overrideIp}");
            }

            return parsedOverride.ToString();
        }

        string[] octets = localIp.Split('.');
        if (octets.Length != 4 ||
            !IPAddress.TryParse(localIp, out IPAddress? parsedLocalIp) ||
            parsedLocalIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new InvalidOperationException($"IPv4 tidak valid: {localIp}");
        }

        return $"{octets[0]}.{octets[1]}.{octets[2]}.90";
    }
}
