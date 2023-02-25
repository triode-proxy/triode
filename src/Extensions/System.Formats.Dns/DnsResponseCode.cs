namespace System.Formats.Dns;

public enum DnsResponseCode : ushort
{
    NoError        = 0,
    FormatError    = 1,
    ServerFailure  = 2,
    NameError      = 3,
    NotImplemented = 4,
    Refused        = 5,
}
