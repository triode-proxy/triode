namespace System.Security.Cryptography.X509Certificates;

public static partial class X509Certificate2Extensions
{
    public static X509Certificate2 CopyWithKeyStorageFlags(this X509Certificate2 certificate, X509KeyStorageFlags keyStorageFlags)
    {
        return new(certificate.Export(X509ContentType.Pkcs12), default(string), keyStorageFlags);
    }

    public static string? GetAuthorityKeyIdentifier(this X509Certificate2 certificate)
    {
        var rawData = certificate.Extensions.FirstOrDefault(x => x.Oid?.Value == Oids.AuthorityKeyIdentifier)?.RawData;
        if (rawData is null)
            return null;
        var (_, content) = new AsnReader(rawData, AsnEncodingRules.BER).ReadSequence().ReadValues()
            .FirstOrDefault(v => v.Tag.TagClass == TagClass.ContextSpecific && v.Tag.TagValue == 0);
        if (content.IsEmpty)
            return null;
        return Convert.ToHexString(content.Span);
    }

    public static bool? GetCertificateAuthority(this X509Certificate2 certificate)
    {
        return certificate.Extensions.OfType<X509BasicConstraintsExtension>().FirstOrDefault()?.CertificateAuthority;
    }

    public static IEnumerable<Uri> GetCrlDistributionPoints(this X509Certificate2 certificate)
    {
        var rawData = certificate.Extensions.FirstOrDefault(x => x.Oid?.Value == Oids.CrlDistributionPoints)?.RawData;
        if (rawData is null)
            yield break;
        var reader = new AsnReader(rawData, AsnEncodingRules.BER).ReadSequence();
        while (reader.HasData)
        {
            var uri = reader.ReadSequence()
                .ReadSequence(new(TagClass.ContextSpecific, 0))
                .ReadSequence(new(TagClass.ContextSpecific, 0))
                .ReadCharacterString(UniversalTagNumber.IA5String, new(TagClass.ContextSpecific, 6));
            yield return new(uri, UriKind.Absolute);
        }
    }

    public static IEnumerable<Oid> GetEnhancedKeyUsages(this X509Certificate2 certificate)
    {
        return certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault()?.EnhancedKeyUsages?.Cast<Oid>() ?? Array.Empty<Oid>();
    }

    public static X509KeyUsageFlags GetKeyUsages(this X509Certificate2 certificate)
    {
        return certificate.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault()?.KeyUsages ?? default;
    }

    public static IEnumerable<string> GetSubjectAltNames(this X509Certificate2 certificate)
    {
        var rawData = certificate.Extensions.FirstOrDefault(x => x.Oid?.Value == Oids.SubjectAltName)?.RawData;
        if (rawData is null)
            return Array.Empty<string>();
        return new AsnReader(rawData, AsnEncodingRules.BER).ReadSequence().ReadValues()
            .Where(v => v.Tag.TagClass == TagClass.ContextSpecific)
            .Select(v => v.Tag.TagValue switch
            {
                2 => $"DNS:{Encoding.ASCII.GetString(v.Content.Span)}",
                7 => $"IP:{new IPAddress(v.Content.Span)}",
                _ => $"[{v.Tag.TagValue}]:{Convert.ToHexString(v.Content.Span)}",
            });
    }

    public static string? GetSubjectKeyIdentifier(this X509Certificate2 certificate)
    {
        return certificate.Extensions.OfType<X509SubjectKeyIdentifierExtension>().FirstOrDefault()?.SubjectKeyIdentifier;
    }

    public static AsymmetricAlgorithm? GetPrivateKey(this X509Certificate2 certificate)
    {
        return certificate.GetRSAPrivateKey()
            ?? certificate.GetDSAPrivateKey()
            ?? certificate.GetECDsaPrivateKey()
            ?? certificate.GetECDiffieHellmanPrivateKey() as AsymmetricAlgorithm;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(this X509Certificate2 certificate, DateTime time)
    {
        return certificate.NotBefore <= time && time < certificate.NotAfter;
    }
}
