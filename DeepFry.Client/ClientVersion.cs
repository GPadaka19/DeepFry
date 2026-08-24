using System.Reflection;

namespace DeepFry.Client;

internal static class ClientVersion
{
    public static string Display =>
        typeof(ClientVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? typeof(ClientVersion).Assembly.GetName().Version?.ToString()
        ?? "unknown";
}
