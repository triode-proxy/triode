namespace System.Formats.Dns;

public static partial class DnsResponseCodeExtensions
{
    public static string AsString(this DnsResponseCode code) => code switch
    {
        DnsResponseCode.NoError        => "NOERROR",
        DnsResponseCode.FormatError    => "FORMERR",
        DnsResponseCode.ServerFailure  => "SERVFAIL",
        DnsResponseCode.NameError      => "NXDOMAIN",
        DnsResponseCode.NotImplemented => "NOTIMP",
        DnsResponseCode.Refused        => "REFUSED",
        _                              => $"{code}",
    };
}
