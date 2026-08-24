using System.Text.Json;

namespace DeepFry.Protocol;

public sealed class RequestMessage
{
    public string RequestId { get; init; } = string.Empty;

    public MessageType Type { get; init; }

    public JsonElement Payload { get; init; }

    public static RequestMessage Create<TPayload>(
        MessageType type,
        TPayload payload,
        string? requestId = null)
    {
        return new RequestMessage
        {
            RequestId = requestId ?? Guid.NewGuid().ToString("N"),
            Type = type,
            Payload = JsonSerializer.SerializeToElement(
                payload,
                ProtocolJson.Options)
        };
    }

    public static RequestMessage Create(
        MessageType type,
        string? requestId = null)
    {
        return Create(type, new EmptyPayload(), requestId);
    }

    public TPayload? GetPayload<TPayload>()
    {
        return Payload.Deserialize<TPayload>(ProtocolJson.Options);
    }

    public bool TryGetPayload<TPayload>(out TPayload? payload)
    {
        try
        {
            payload = GetPayload<TPayload>();
            return payload is not null;
        }
        catch (InvalidOperationException)
        {
            payload = default;
            return false;
        }
        catch (JsonException)
        {
            payload = default;
            return false;
        }
    }

    private sealed class EmptyPayload
    {
    }
}
