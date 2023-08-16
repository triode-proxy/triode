namespace System.Formats.Dns;

public readonly struct DnsName
{
    internal Memory<byte> Packet { get; }

    internal int Offset { get; }

    public Memory<byte> Memory => Packet.Slice(Offset, Length);

    public Span<byte> Span => Memory.Span;

    public bool IsPointer => Offset < Packet.Length && (Packet.Span[Offset] & 0xC0) == 0xC0;

    public int Length => IsPointer ? 2 : Labels.Sum(label => 1 + label.Length) + 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal DnsName(Memory<byte> packet, int offset) => (Packet, Offset) = (packet, offset);

    public IEnumerable<Memory<byte>> Labels
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var offset = IsPointer ? ReadUInt16BigEndian(Packet.Span[Offset..]) & 0x3FFF : Offset;
            while (offset < Packet.Length)
            {
                var length = Packet.Span[offset++];
                if (length == 0)
                    break;
                yield return Packet.Slice(offset, length);
                offset += length;
            }
        }
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        foreach (var label in Labels)
        {
            if (builder.Length != 0)
                builder.Append('.');
            builder.Append(Encoding.ASCII.GetString(label.Span));
        }
        return builder.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetByteCount(ReadOnlySpan<char> name) => 1 + Encoding.ASCII.GetByteCount(name) + 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBytes(ReadOnlySpan<char> name, Span<byte> bytes)
    {
        var length = GetByteCount(name);
        if (length > 255)
            throw new ArgumentException("Name too long", nameof(name));
        Encoding.ASCII.GetBytes(name, bytes.Slice(1, length));
        bytes[length - 1] = 0;
        for (int p = 0; p < length - 1; )
        {
            var n = bytes[(p + 1)..(length - 1)].IndexOf((byte)'.');
            if (n < 0)
                n = (length - 1) - (p + 1);
            if (n > 63)
                throw new ArgumentException("Label too long", nameof(name));
            bytes[p] = (byte)n;
            p += 1 + n;
        }
        return length;
    }
}
