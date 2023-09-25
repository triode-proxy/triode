namespace System.Security.Cryptography.X509Certificates;

public sealed partial class CertificateRevocationListBuilder
{
    public byte[] Build(
        X509Certificate2 issuerCertificate,
        BigInteger crlNumber,
        DateTimeOffset nextUpdate,
        HashAlgorithmName hashAlgorithm,
        RSASignaturePadding? rsaSignaturePadding = null,
        DateTimeOffset? thisUpdate = null)
    {
        return Build(
            issuerCertificate,
            crlNumber,
            nextUpdate,
            thisUpdate.GetValueOrDefault(DateTimeOffset.UtcNow),
            hashAlgorithm,
            rsaSignaturePadding);
    }

    private byte[] Build(
        X509Certificate2 issuerCertificate,
        BigInteger crlNumber,
        DateTimeOffset nextUpdate,
        DateTimeOffset thisUpdate,
        HashAlgorithmName hashAlgorithm,
        RSASignaturePadding? rsaSignaturePadding)
    {
        ArgumentNullException.ThrowIfNull(issuerCertificate);
        if (!issuerCertificate.HasPrivateKey)
            throw new ArgumentException("The provided issuer certificate does not have an associated private key.", nameof(issuerCertificate));
        if (crlNumber < 0)
            throw new ArgumentOutOfRangeException(nameof(crlNumber), "Non-negative number required.");
        if (nextUpdate <= thisUpdate)
            throw new ArgumentException("The provided thisUpdate value is later than the nextUpdate value.");
        if (string.IsNullOrEmpty(hashAlgorithm.Name))
            throw new ArgumentException("The value cannot be an empty string.", nameof(hashAlgorithm));
        if (issuerCertificate.GetCertificateAuthority() != true)
            throw new ArgumentException("The issuer certificate does not have an appropriate value for the Basic Constraints extension.", nameof(issuerCertificate));
        var keyUsages = issuerCertificate.GetKeyUsages();
        if (keyUsages != 0 && (keyUsages & X509KeyUsageFlags.CrlSign) == 0)
            throw new ArgumentException("The issuer certificate's Key Usage extension does not contain the CrlSign flag.", nameof(issuerCertificate));
        var subjectKeyIdentifier = issuerCertificate.GetSubjectKeyIdentifier() ??
            throw new ArgumentException("The issuer certificate does not have the Subject Key Identifier extension.", nameof(issuerCertificate));
        if (issuerCertificate.GetKeyAlgorithm() != Oids.Rsa)
            throw new ArgumentException($"'{issuerCertificate.GetKeyAlgorithm()}' is not a known key algorithm.", nameof(issuerCertificate));
        ArgumentNullException.ThrowIfNull(rsaSignaturePadding);

        using var key = issuerCertificate.GetRSAPrivateKey()!;
        var generator = X509SignatureGenerator.CreateForRSA(key, rsaSignaturePadding);
        var akid = new X509AuthorityKeyIdentifierExtension(subjectKeyIdentifier, false);

        return Build(issuerCertificate.SubjectName, generator, crlNumber, nextUpdate, thisUpdate, hashAlgorithm, akid);
    }

    [SuppressMessage("Performance", "CA1822", Justification = "Partially backported")]
    private byte[] Build(
        X500DistinguishedName issuerName,
        X509SignatureGenerator generator,
        BigInteger crlNumber,
        DateTimeOffset nextUpdate,
        DateTimeOffset thisUpdate,
        HashAlgorithmName hashAlgorithm,
        X509AuthorityKeyIdentifierExtension authorityKeyIdentifier)
    {
        ArgumentNullException.ThrowIfNull(issuerName);
        ArgumentNullException.ThrowIfNull(generator);
        if (crlNumber < 0)
            throw new ArgumentOutOfRangeException(nameof(crlNumber), "Non-negative number required.");
        if (nextUpdate <= thisUpdate)
            throw new ArgumentException("The provided thisUpdate value is later than the nextUpdate value.");
        if (string.IsNullOrEmpty(hashAlgorithm.Name))
            throw new ArgumentException("The value cannot be an empty string.", nameof(hashAlgorithm));
        ArgumentNullException.ThrowIfNull(authorityKeyIdentifier);

        var signatureAlgId = generator.GetSignatureAlgorithmIdentifier(hashAlgorithm);

        var writer = new AsnWriter(AsnEncodingRules.DER);

        // TBSCertList
        using (writer.PushSequence())
        {
            // version v2(1)
            writer.WriteInteger(1);

            // signature (AlgorithmIdentifier)
            writer.WriteEncodedValue(signatureAlgId);

            // issuer
            writer.WriteEncodedValue(issuerName.RawData);

            // thisUpdate
            writer.WriteUtcTime(thisUpdate);

            // nextUpdate
            writer.WriteUtcTime(nextUpdate);

            // extensions [0] EXPLICIT Extensions
            using (writer.PushSequence(new(TagClass.ContextSpecific, 0)))
            {
                // Extensions (SEQUENCE OF)
                using (writer.PushSequence())
                {
                    // Authority Key Identifier Extension
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier(authorityKeyIdentifier.Oid!.Value!);
                        if (authorityKeyIdentifier.Critical)
                            writer.WriteBoolean(true);
                        writer.WriteOctetString(authorityKeyIdentifier.RawData);
                    }

                    // CRL Number Extension
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier(Oids.CrlNumber);
                        using (writer.PushOctetString())
                            writer.WriteInteger(crlNumber);
                    }
                }
            }
        }

        var tbsCertList = writer.Encode();
        var signature = generator.SignData(tbsCertList, hashAlgorithm);

        writer.Reset();

        // CertificateList
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(tbsCertList);
            writer.WriteEncodedValue(signatureAlgId);
            writer.WriteBitString(signature);
        }
        return writer.Encode();
    }
}
