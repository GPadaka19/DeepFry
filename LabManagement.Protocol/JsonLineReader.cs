using System.Buffers;
using System.Text;

namespace LabManagement.Protocol;

public sealed class JsonLineReader : IDisposable
{
    private readonly Stream _stream;
    private readonly byte[] _readBuffer;
    private readonly ArrayBufferWriter<byte> _lineBuffer = new();
    private readonly int _maximumLineBytes;

    private int _bufferOffset;
    private int _bufferCount;

    public JsonLineReader(
        Stream stream,
        int maximumLineBytes = 64 * 1024)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (maximumLineBytes <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maximumLineBytes));

        _stream = stream;
        _maximumLineBytes = maximumLineBytes;
        _readBuffer = new byte[Math.Min(4096, maximumLineBytes)];
    }

    public async Task<string?> ReadLineAsync(
        CancellationToken cancellationToken)
    {
        while (true)
        {
            if (_bufferOffset < _bufferCount)
            {
                int availableBytes = _bufferCount - _bufferOffset;
                int lineFeedOffset = Array.IndexOf(
                    _readBuffer,
                    (byte)'\n',
                    _bufferOffset,
                    availableBytes);

                if (lineFeedOffset >= 0)
                {
                    Append(_readBuffer.AsSpan(
                        _bufferOffset,
                        lineFeedOffset - _bufferOffset));

                    _bufferOffset = lineFeedOffset + 1;
                    string line = Encoding.UTF8.GetString(
                        _lineBuffer.WrittenSpan);
                    _lineBuffer.Clear();
                    return line;
                }

                Append(_readBuffer.AsSpan(
                    _bufferOffset,
                    availableBytes));
                _bufferOffset = _bufferCount;
            }

            _bufferCount = await _stream.ReadAsync(
                _readBuffer,
                cancellationToken);
            _bufferOffset = 0;

            if (_bufferCount == 0)
            {
                if (_lineBuffer.WrittenCount == 0)
                    return null;

                _lineBuffer.Clear();
                throw new InvalidDataException(
                    "Connection ended before a complete message was received.");
            }
        }
    }

    public void Dispose()
    {
        _lineBuffer.Clear();
    }

    private void Append(ReadOnlySpan<byte> bytes)
    {
        if (_lineBuffer.WrittenCount + bytes.Length >
            _maximumLineBytes)
        {
            _lineBuffer.Clear();
            throw new InvalidDataException(
                $"Message exceeds the {_maximumLineBytes}-byte limit.");
        }

        Span<byte> destination = _lineBuffer.GetSpan(bytes.Length);
        bytes.CopyTo(destination);
        _lineBuffer.Advance(bytes.Length);
    }
}
