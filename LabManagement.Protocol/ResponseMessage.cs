using System.Text.Json;

namespace LabManagement.Protocol;

public sealed class ResponseMessage
{
    public string RequestId { get; init; } = string.Empty;

    public MessageType Type { get; init; } = MessageType.Response;

    public bool Success { get; init; }

    public JsonElement Payload { get; init; }

    public ErrorInfo? Error { get; init; }

    public static ResponseMessage CreateSuccess<TPayload>(
        string requestId,
        TPayload payload)
    {
        return new ResponseMessage
        {
            RequestId = requestId,
            Success = true,
            Payload = JsonSerializer.SerializeToElement(
                payload,
                ProtocolJson.Options)
        };
    }

    public static ResponseMessage CreateError(
        string requestId,
        ErrorInfo error)
    {
        return new ResponseMessage
        {
            RequestId = requestId,
            Success = false,
            Error = error,
            Payload = JsonSerializer.SerializeToElement(
                new EmptyPayload(),
                ProtocolJson.Options)
        };
    }

    public TPayload? GetPayload<TPayload>()
    {
        return Payload.Deserialize<TPayload>(ProtocolJson.Options);
    }

    private sealed class EmptyPayload
    {
    }
}
