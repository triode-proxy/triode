namespace System.Formats.Dns;

public struct DnsHeader
{
    private static readonly RandomNumberGenerator RNG = RandomNumberGenerator.Create();

    public const int Length = 12;

    public Memory<byte> Packet { get; }

    public Memory<byte> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Packet[..Length];
    }

    public Span<byte> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Memory.Span;
    }

    public ushort Id
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ReadUInt16BigEndian(Span[0..2]);
    }

    public DnsPacketType PacketType
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (DnsPacketType)((ReadUInt16BigEndian(Span[2..4]) & 0x8000) >> 15);
    }

    public DnsOperationCode OperationCode
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (DnsOperationCode)((ReadUInt16BigEndian(Span[2..4]) & 0x7800) >> 11);
    }

    public bool IsAuthoritativeAnswer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ReadUInt16BigEndian(Span[2..4]) & 0x0400) != 0;
    }

    public bool IsTruncated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ReadUInt16BigEndian(Span[2..4]) & 0x0200) != 0;
    }

    public bool IsRecursionDesired
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ReadUInt16BigEndian(Span[2..4]) & 0x0100) != 0;
    }

    public bool IsRecursionAvailable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ReadUInt16BigEndian(Span[2..4]) & 0x0080) != 0;
    }

    public DnsResponseCode ResponseCode
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (DnsResponseCode)((ReadUInt16BigEndian(Span[2..4]) & 0x000F) >> 0);
    }

    public ushort QuestionCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ReadUInt16BigEndian(Span[4..6]);
    }

    public ushort AnswerCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ReadUInt16BigEndian(Span[6..8]);
    }

    public ushort AuthorityCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ReadUInt16BigEndian(Span[8..10]);
    }

    public ushort AdditionalCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ReadUInt16BigEndian(Span[10..12]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal DnsHeader(Memory<byte> packet) => Packet = packet[0..Length];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(Memory<byte> packet,
        ushort? id,
        DnsPacketType qr,
        DnsOperationCode opcode = DnsOperationCode.StandardQuery,
        bool aa = false,
        bool tc = false,
        bool rd = false,
        bool ra = false,
        DnsResponseCode rcode = DnsResponseCode.NoError,
        ushort qdcount = 0,
        ushort ancount = 0,
        ushort nscount = 0,
        ushort arcount = 0)
    {
        if (id is ushort qid)
            WriteUInt16BigEndian(packet.Span[0..2], qid);
        else
            RNG.GetBytes(packet.Span[0..2]);
        WriteUInt16BigEndian(packet.Span[2..4], (ushort)(
            (int)qr      << 15 & 0x8000 |
            (int)opcode  << 11 & 0x7800 |
            (aa ? 1 : 0) << 10 & 0x0400 |
            (tc ? 1 : 0) <<  9 & 0x0200 |
            (rd ? 1 : 0) <<  8 & 0x0100 |
            (ra ? 1 : 0) <<  7 & 0x0080 |
            (int)rcode   <<  0 & 0x000F
        ));
        WriteUInt16BigEndian(packet.Span[ 4.. 6], qdcount);
        WriteUInt16BigEndian(packet.Span[ 6.. 8], ancount);
        WriteUInt16BigEndian(packet.Span[ 8..10], nscount);
        WriteUInt16BigEndian(packet.Span[10..12], arcount);
    }
}
