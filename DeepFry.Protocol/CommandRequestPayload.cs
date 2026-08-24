using System.Text.Json;

namespace DeepFry.Protocol;

public sealed class CommandRequestPayload
{
    public string Name { get; init; } = string.Empty;

    public JsonElement Arguments { get; init; } =
        JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options);
}
