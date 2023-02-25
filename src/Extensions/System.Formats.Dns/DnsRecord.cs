namespace System.Formats.Dns;

public readonly struct DnsRecord
{
    internal Memory<byte> Packet { get; }

    internal int Offset { get; }

    public Memory<byte> Memory => Packet.Slice(Offset, Length);

    public Span<byte> Span => Memory.Span;

    public int Length => Name.Length + sizeof(DnsRecordType) + sizeof(DnsRecordClass) + sizeof(uint) + sizeof(ushort) + Data.Length;

    public DnsName Name => new(Packet, Offset);

    public DnsRecordType Type => (DnsRecordType)ReadUInt16BigEndian(Packet.Slice(Offset + Name.Length, 2).Span);

    public DnsRecordClass Class => (DnsRecordClass)ReadUInt16BigEndian(Packet.Slice(Offset + Name.Length + 2, 2).Span);

    public TimeSpan TimeToLive => TimeSpan.FromSeconds(ReadUInt32BigEndian(Packet.Slice(Offset + Name.Length + 4, 4).Span));

    public Memory<byte> Data
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var offset = Offset + Name.Length + 8;
            var length = ReadUInt16BigEndian(Packet.Slice(offset, 2).Span);
            return Packet.Slice(offset + 2, length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal DnsRecord(Memory<byte> packet, int offset) => (Packet, Offset) = (packet, offset);
}
