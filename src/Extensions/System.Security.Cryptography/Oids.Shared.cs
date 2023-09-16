namespace System.Security.Cryptography;

internal static partial class Oids
{
    internal static readonly Oid CrlDistributionPointsOid = InitializeOid(CrlDistributionPoints);

    internal static Oid InitializeOid(string value)
    {
        var oid = new Oid(value, null);
        _ = oid.FriendlyName;
        return oid;
    }
}
