namespace System.Formats.Dns;

public readonly struct DnsPacket
{
    private static readonly RandomNumberGenerator RNG = RandomNumberGenerator.Create();

    public Memory<byte> Memory { get; }

    public Span<byte> Span => Memory.Span;

    public DnsHeader Header => new(Memory);

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

    [SuppressMessage("Performance", "CA1806")]
    public static DnsPacket CreateQuestion(ReadOnlySpan<char> name, DnsRecordType type, DnsRecordClass @class = DnsRecordClass.IN)
    {
        var length = DnsHeader.Length + DnsName.GetByteCount(name) + sizeof(DnsRecordType) + sizeof(DnsRecordClass);
        var packet = new DnsPacket(new byte[length]);
        RNG.GetBytes(packet.Span[0..2]);
        new DnsHeader(packet.Memory)
        {
            IsRecursionDesired = true,
            QuestionCount      = 1,
        };
        var n = DnsName.GetBytes(name, packet.Span[DnsHeader.Length..]);
        WriteUInt16BigEndian(packet.Span.Slice(DnsHeader.Length + n + 0, 2), (ushort)type);
        WriteUInt16BigEndian(packet.Span.Slice(DnsHeader.Length + n + 2, 2), (ushort)@class);
        return packet;
    }

    [SuppressMessage("Performance", "CA1806")]
    public static DnsPacket CreateAnswer(DnsQuestion question, IEnumerable<(DnsRecordType Type, DnsRecordClass Class, TimeSpan TimeToLive, ReadOnlyMemory<byte> Data)> records)
    {
        var source = new DnsPacket(question.Packet);
        var offset = DnsHeader.Length + question.Length;
        var length = offset + records.Sum(r => sizeof(ushort) + sizeof(DnsRecordType) + sizeof(DnsRecordClass) + sizeof(uint) + sizeof(ushort) + r.Data.Length);
        var packet = new DnsPacket(new byte[length]);
        new DnsHeader(packet.Memory)
        {
            Id                   = source.Header.Id,
            IsResponse           = true,
            OperationCode        = source.Header.OperationCode,
            IsRecursionDesired   = source.Header.IsRecursionDesired,
            IsRecursionAvailable = true,
            QuestionCount        = 1,
            AnswerCount          = (ushort)records.Count(),
        };
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
    public static DnsPacket CreateAnswer(DnsQuestion question, DnsRecordType type, TimeSpan ttl, IEnumerable<IPAddress> addresses)
    {
        Debug.Assert(type == DnsRecordType.A    && addresses.All(a => a.AddressFamily == AddressFamily.InterNetwork)
                  || type == DnsRecordType.AAAA && addresses.All(a => a.AddressFamily == AddressFamily.InterNetworkV6));
        return CreateAnswer(question, addresses.Select(a => (type, DnsRecordClass.IN, ttl, new ReadOnlyMemory<byte>(a.GetAddressBytes()))));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DnsPacket CreateAnswer(DnsQuestion question, DnsRecordType type, TimeSpan ttl, IPAddress address)
    {
        return CreateAnswer(question, type, ttl, new[] { address });
    }

    [SuppressMessage("Performance", "CA1806")]
    public static DnsPacket CreateError(DnsHeader header, DnsResponseCode code)
    {
        var packet = new DnsPacket(new byte[DnsHeader.Length]);
        new DnsHeader(packet.Memory)
        {
            Id                   = header.Id,
            IsResponse           = true,
            OperationCode        = header.OperationCode,
            IsRecursionDesired   = header.IsRecursionDesired,
            IsRecursionAvailable = true,
            ResponseCode         = code,
        };
        return packet;
    }
}
