namespace System.Formats.Dns;

[SuppressMessage("Naming", "CA1720")]
public enum DnsRecordType : ushort
{
    A     = 0x0001,
    NS    = 0x0002,
    CNAME = 0x0005,
    SOA   = 0x0006,
    PTR   = 0x000C,
    MX    = 0x000F,
    TXT   = 0x0010,
    AAAA  = 0x001C,
}
