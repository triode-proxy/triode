namespace System.Formats.Dns;

public readonly struct DnsQuestion
{
    internal Memory<byte> Packet { get; }

    internal int Offset { get; }

    public Memory<byte> Memory => Packet.Slice(Offset, Length);

    public Span<byte> Span => Memory.Span;

    public int Length => Name.Length + sizeof(DnsRecordType) + sizeof(DnsRecordClass);

    public DnsName Name => new(Packet, Offset);

    public DnsRecordType Type => (DnsRecordType)ReadUInt16BigEndian(Packet.Slice(Offset + Name.Length, 2).Span);

    public DnsRecordClass Class => (DnsRecordClass)ReadUInt16BigEndian(Packet.Slice(Offset + Name.Length + 2, 2).Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal DnsQuestion(Memory<byte> packet, int offset) => (Packet, Offset) = (packet, offset);
}
