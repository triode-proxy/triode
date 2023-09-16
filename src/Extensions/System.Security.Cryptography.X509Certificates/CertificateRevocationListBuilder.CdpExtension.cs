namespace System.Security.Cryptography.X509Certificates;

public sealed partial class CertificateRevocationListBuilder
{
    public static X509Extension BuildCrlDistributionPointExtension(IEnumerable<string> uris, bool critical = false)
    {
        ArgumentNullException.ThrowIfNull(uris);

        AsnWriter? writer = null;
        foreach (var uri in uris)
        {
            if (uri is null)
                throw new ArgumentException("One of the provided CRL Distribution Point URIs is a null value.", nameof(uris));

            if (writer is null)
            {
                writer = new AsnWriter(AsnEncodingRules.DER);
                // CRLDistributionPoints
                writer.PushSequence();
            }

            // DistributionPoint
            using (writer.PushSequence())
            {
                // DistributionPoint/DistributionPointName EXPLICIT [0]
                using (writer.PushSequence(new(TagClass.ContextSpecific, 0)))
                {
                    // DistributionPointName/GeneralName
                    using (writer.PushSequence(new(TagClass.ContextSpecific, 0)))
                    {
                        // GeneralName/Uri
                        try
                        {
                            writer.WriteCharacterString(UniversalTagNumber.IA5String, uri, new(TagClass.ContextSpecific, 6));
                        }
                        catch (EncoderFallbackException e)
                        {
                            throw new CryptographicException("The string contains a character not in the 7 bit ASCII character set.", e);
                        }
                    }
                }
            }
        }

        if (writer is null)
            throw new ArgumentException("The collection of distribution URIs must be non-empty.", nameof(uris));

        // CRLDistributionPoints
        writer.PopSequence();

        return new(Oids.CrlDistributionPointsOid, writer.Encode(), critical);
    }
}
