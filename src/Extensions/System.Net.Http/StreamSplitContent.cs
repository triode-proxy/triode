namespace System.Net.Http;

public sealed class StreamSplitContent : HttpContent
{
    private const int BufferSize = 8192;

    private readonly Stream _source;
    private readonly Stream _target;
    private readonly long _capacity;
    private readonly CancellationToken _aborted;

    public StreamSplitContent(Stream source, Stream target, long? capacity = null, CancellationToken aborted = default)
    {
        _source = source;
        _target = target;
        _capacity = capacity ?? long.MaxValue;
        _aborted = aborted;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        using var buffer = MemoryPool<byte>.Shared.Rent(BufferSize);
        var written = 0L;
        while (_source.CanRead)
        {
            var read = await _source.ReadAsync(buffer.Memory, _aborted).ConfigureAwait(false);
            if (read <= 0)
                break;
            await stream.WriteAsync(buffer.Memory[..read], _aborted).ConfigureAwait(false);
            if (written < _capacity)
            {
                var cap = (int)Math.Min(read, _capacity - written);
                await _target.WriteAsync(buffer.Memory[..cap], _aborted).ConfigureAwait(false);
            }
            written += read;
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        if (_source.CanSeek)
        {
            length = _source.Length;
            return true;
        }
        length = default;
        return false;
    }
}
