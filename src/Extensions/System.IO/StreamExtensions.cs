namespace System.IO;

public static partial class StreamExtensions
{
    public static async ValueTask ReadExactlyAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        for (var offset = 0; offset < buffer.Length;)
        {
            var n = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (n == 0)
                throw new EndOfStreamException();
            offset += n;
        }
    }
}
