namespace System.Formats.Dns;

public readonly struct DnsPacket
{
    public Memory<byte> Memory { get; }

    public Span<byte> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Memory.Span;
    }

    public DnsHeader Header
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Memory);
    }

    public DnsPacketType Type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Header.PacketType;
    }

    public IEnumerable<DnsQuestion> Questions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var count = Header.QuestionCount;
            var offset = DnsHeader.Length;
            for (int i = 0; i < count && offset < Memory.Length; i++)
            {
                var question = new DnsQuestion(Memory, offset);
                yield return question;
                offset += question.Length;
            }
        }
    }

    public IEnumerable<DnsRecord> Answers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var count = Header.AnswerCount;
            if (count == 0)
                yield break;
            var offset = DnsHeader.Length + Questions.Sum(q => q.Length);
            for (int i = 0; i < count && offset < Memory.Length; i++)
            {
                var record = new DnsRecord(Memory, offset);
                yield return record;
                offset += record.Length;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DnsPacket(Memory<byte> buffer) => Memory = buffer;

    public static DnsPacket CreateQuery(ReadOnlySpan<char> name, DnsRecordType type, DnsRecordClass @class = DnsRecordClass.IN)
    {
        var length = DnsHeader.Length + DnsName.GetByteCount(name) + sizeof(DnsRecordType) + sizeof(DnsRecordClass);
        var packet = new DnsPacket(new byte[length]);
        DnsHeader.GetBytes(packet.Memory, null, DnsPacketType.Query, rd: true, qdcount: 1);
        var n = DnsName.GetBytes(name, packet.Span[DnsHeader.Length..]);
        WriteUInt16BigEndian(packet.Span.Slice(DnsHeader.Length + n + 0, 2), (ushort)type);
        WriteUInt16BigEndian(packet.Span.Slice(DnsHeader.Length + n + 2, 2), (ushort)@class);
        return packet;
    }

    public static DnsPacket CreateResponse(DnsQuestion question, IEnumerable<(DnsRecordType Type, DnsRecordClass Class, TimeSpan TimeToLive, ReadOnlyMemory<byte> Data)> records)
    {
        var source = new DnsPacket(question.Packet);
        var header = source.Header;
        var offset = DnsHeader.Length + question.Length;
        var length = offset + records.Sum(r => sizeof(ushort) + sizeof(DnsRecordType) + sizeof(DnsRecordClass) + sizeof(uint) + sizeof(ushort) + r.Data.Length);
        var packet = new DnsPacket(new byte[length]);
        DnsHeader.GetBytes(packet.Memory, header.Id, DnsPacketType.Response,
            opcode : header.OperationCode,
            rd     : header.IsRecursionDesired,
            ra     : true,
            qdcount: 1,
            ancount: (ushort)records.Count());
        question.Span.CopyTo(packet.Span[DnsHeader.Length..]);
        foreach (var (type, @class, ttl, data) in records)
        {
            WriteUInt16BigEndian(packet.Span.Slice(offset +  0, 2), (ushort)(0xC000 | DnsHeader.Length));
            WriteUInt16BigEndian(packet.Span.Slice(offset +  2, 2), (ushort)type);
            WriteUInt16BigEndian(packet.Span.Slice(offset +  4, 2), (ushort)@class);
            WriteUInt32BigEndian(packet.Span.Slice(offset +  6, 4), (uint)ttl.TotalSeconds);
            WriteUInt16BigEndian(packet.Span.Slice(offset + 10, 2), (ushort)data.Length);
            data.CopyTo(packet.Memory[(offset + 12)..]);
            offset += 12 + data.Length;
        }
        return packet;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DnsPacket CreateResponse(DnsQuestion question, TimeSpan ttl, IEnumerable<IPAddress> addresses)
    {
        Debug.Assert(addresses.All(a => a.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6));
        return CreateResponse(question, addresses.Select(a => (a.AddressFamily == AddressFamily.InterNetwork ? DnsRecordType.A : DnsRecordType.AAAA, DnsRecordClass.IN, ttl, new ReadOnlyMemory<byte>(a.GetAddressBytes()))));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DnsPacket CreateResponse(DnsQuestion question, TimeSpan ttl, params IPAddress[] addresses)
    {
        return CreateResponse(question, ttl, addresses.AsEnumerable());
    }

    public static DnsPacket CreateResponse(DnsHeader header, DnsResponseCode rcode)
    {
        var packet = new DnsPacket(new byte[DnsHeader.Length]);
        DnsHeader.GetBytes(packet.Memory, header.Id, DnsPacketType.Response,
            opcode: header.OperationCode,
            rd    : header.IsRecursionDesired,
            ra    : true,
            rcode : rcode);
        return packet;
    }
}
