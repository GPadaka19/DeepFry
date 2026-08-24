using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DeepFry.Host;

public static class LabNetworkDiscovery
{
    public const int ClientPort = 5020;

    public static IReadOnlyList<string> GetCandidateClientAddresses()
    {
        var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            IPAddress.Loopback.ToString()
        };

        foreach (NetworkInterface networkInterface
                 in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (UnicastIPAddressInformation unicast
                     in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                foreach (string address in BuildClientAddresses(
                             unicast.Address.ToString()))
                {
                    addresses.Add(address);
                }
            }
        }

        return addresses.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IReadOnlyList<string> BuildClientAddresses(string hostIp)
    {
        if (!IPAddress.TryParse(hostIp, out IPAddress? address) ||
            address.AddressFamily != AddressFamily.InterNetwork)
        {
            return [];
        }

        byte[] octets = address.GetAddressBytes();
        if (octets[0] != 10 || octets[3] != 90)
            return [];

        return Enumerable.Range(1, 89)
            .Select(lastOctet =>
                $"{octets[0]}.{octets[1]}.{octets[2]}.{lastOctet}")
            .ToArray();
    }
}
