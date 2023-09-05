namespace System.Formats.Dns;

[SuppressMessage("Naming", "CA1720")]
public enum DnsRecordType : ushort
{
    A     = 1,
    NS    = 2,
    CNAME = 5,
    SOA   = 6,
    PTR   = 12,
    MX    = 15,
    TXT   = 16,
    AAAA  = 28,
    ANY   = 255,
}
