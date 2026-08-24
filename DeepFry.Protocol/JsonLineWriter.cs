using System.Text;
using System.Text.Json;

namespace DeepFry.Protocol;

public sealed class JsonLineWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public JsonLineWriter(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
    }

    public async Task WriteAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(
            message,
            ProtocolJson.Options);
        byte[] data = Encoding.UTF8.GetBytes(json + "\n");

        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            await _stream.WriteAsync(data, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        _writeLock.Dispose();
    }
}
