namespace System.Formats.Dns;

public struct DnsHeader
{
    public const int Length = 12;

    public Memory<byte> Packet { get; }

    public Memory<byte> Memory => Packet[..Length];

    public Span<byte> Span => Memory.Span;

    public ushort Id
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ReadUInt16BigEndian(Span[0..2]);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => WriteUInt16BigEndian(Span[0..2], value);
    }

    public bool IsResponse
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ReadUInt16BigEndian(Span[2..4]) & 0x8000) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => WriteUInt16BigEndian(Span[2..4], (ushort)(ReadUInt16BigEndian(Span[2..4]) & ~0x8000 | (value ? 0x8000 : 0)));
    }

    public DnsOperationCode OperationCode
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (DnsOperationCode)((ReadUInt16BigEndian(Span[2..4]) & 0x7800) >> 11);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => WriteUInt16BigEndian(Span[2..4], (ushort)(ReadUInt16BigEndian(Span[2..4]) & ~0x7800 | (ushort)value << 11));
    }

    public bool IsAuthoritativeAnswer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ReadUInt16BigEndian(Span[2..4]) & 0x0400) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => WriteUInt16BigEndian(Span[2..4], (ushort)(ReadUInt16BigEndian(Span[2..4]) & ~0x0400 | (value ? 0x0400 : 0)));
    }

    public bool IsTruncated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ReadUInt16BigEndian(Span[2..4]) & 0x0200) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => WriteUInt16BigEndian(Span[2..4], (ushort)(ReadUInt16BigEndian(Span[2..4]) & ~0x0200 | (value ? 0x0200 : 0)));
    }

    public bool IsRecursionDesired
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ReadUInt16BigEndian(Span[2..4]) & 0x0100) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => WriteUInt16BigEndian(Span[2..4], (ushort)(ReadUInt16BigEndian(Span[2..4]) & ~0x0100 | (value ? 0x0100 : 0)));
    }

    public bool IsRecursionAvailable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ReadUInt16BigEndian(Span[2..4]) & 0x0080) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => WriteUInt16BigEndian(Span[2..4], (ushort)(ReadUInt16BigEndian(Span[2..4]) & ~0x0080 | (value ? 0x0080 : 0)));
    }

    public DnsResponseCode ResponseCode
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (DnsResponseCode)((ReadUInt16BigEndian(Span[2..4]) & 0x000F) >> 0);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => WriteUInt16BigEndian(Span[2..4], (ushort)(ReadUInt16BigEndian(Span[2..4]) & ~0x000F | (ushort)value << 0));
    }

    public ushort QuestionCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ReadUInt16BigEndian(Span[4..6]);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => WriteUInt16BigEndian(Span[4..6], value);
    }

    public ushort AnswerCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ReadUInt16BigEndian(Span[6..8]);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => WriteUInt16BigEndian(Span[6..8], value);
    }

    public ushort AuthorityCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ReadUInt16BigEndian(Span[8..10]);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => WriteUInt16BigEndian(Span[8..10], value);
    }

    public ushort AdditionalCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ReadUInt16BigEndian(Span[10..12]);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => WriteUInt16BigEndian(Span[10..12], value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal DnsHeader(Memory<byte> packet) => Packet = packet[0..Length];
}
