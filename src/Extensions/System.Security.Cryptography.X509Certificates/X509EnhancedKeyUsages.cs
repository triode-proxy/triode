namespace System.Security.Cryptography.X509Certificates;

public static partial class X509EnhancedKeyUsages
{
    public static readonly Oid ServerAuth  = Oids.InitializeOid(Oids.ServerAuth);
    public static readonly Oid ClientAuth  = Oids.InitializeOid(Oids.ClientAuth);
    public static readonly Oid CodeSigning = Oids.InitializeOid(Oids.CodeSigning);
}
