internal sealed class Authority : IDisposable
{
    private static readonly TimeSpan CAValidity = TimeSpan.FromDays(3653);
    private static readonly TimeSpan TLSValidity = TimeSpan.FromDays(398);

    private readonly ILogger _logger;
    private readonly X509Store _store;
    private readonly ConcurrentDictionary<string, object> _locks = new();

    private volatile X509Certificate2 _ca;

    public X509Certificate2 Certificate
    {
        get
        {
            var tomorrow = DateTime.Now + TimeSpan.FromDays(1);
            if (_ca.NotAfter < tomorrow)
            {
                lock (_locks)
                {
                    if (_ca.NotAfter < tomorrow)
                        Interlocked.Exchange(ref _ca, IssueAuthorityCertificate()).Dispose();
                }
            }
            return _ca;
        }
    }

    public Authority(ILogger<Authority> logger)
    {
        _logger = logger;
        _store = new(StoreName.My, StoreLocation.CurrentUser);
        _store.Open(OpenFlags.ReadWrite);
        var now = DateTime.Now;
        _ca = _store.Certificates
            .Where(c => c.HasPrivateKey
                     && c.IsValid(now)
                     && c.GetCertificateAuthority() == true
                     && c.GetSubjectKeyIdentifier() is not null)
            .MaxBy(c => c.NotAfter) ?? IssueAuthorityCertificate();
    }

    public void Dispose()
    {
        _ca?.Dispose();
        _store?.Dispose();
        GC.SuppressFinalize(this);
    }

    public X509Certificate2 GetServerCertificate(string cn)
    {
        var ca = Certificate;
        if (FindServerCertificate(cn) is X509Certificate2 cert)
            return cert;
        var obj = _locks.GetOrAdd(cn, _ => new());
        try
        {
            lock (obj)
                return FindServerCertificate(cn) ?? IssueServerCertificate(cn, ca);
        }
        finally
        {
            _locks.TryRemove(cn, out _);
        }
    }

    private X509Certificate2? FindServerCertificate(string cn)
    {
        var now = DateTime.Now;
        var sans = IPAddress.TryParse(cn, out var addr) ? new[] { $"IP:{addr}" }
            : cn.IndexOf('.') is int dot and > 0 ? new[] { $"DNS:{cn}", $"DNS:*{cn.AsSpan(dot)}" }
            : new[] { $"DNS:{cn}" };
        return _store.Certificates
            .Where(c => c.HasPrivateKey
                     && c.IsValid(now)
                     && c.GetEnhancedKeyUsages().Any(oid => oid.Value == Oids.ServerAuth)
                     && c.GetSubjectAltNames().Intersect(sans).Any())
            .MaxBy(c => c.NotAfter);
    }

    private X509Certificate2 IssueServerCertificate(string cn, X509Certificate2 ca)
    {
        var aid = ca.GetSubjectKeyIdentifier() ?? throw new ArgumentException("Subject Key Identifier required", nameof(ca));
        var now = DateTime.Now;
        var validity = TimeSpan.FromDays(Math.Min(TLSValidity.TotalDays, (ca.NotAfter - now).TotalDays));
        var subject = new X500DistinguishedName($"CN={cn}");
        var sans = new SubjectAlternativeNameBuilder();
        if (IPAddress.TryParse(cn, out var addr))
            sans.AddIpAddress(addr);
        else
            sans.AddDnsName(cn);
        using var key = RSA.Create(2048);
        var req = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { X509EnhancedKeyUsages.ServerAuth, X509EnhancedKeyUsages.ClientAuth }, false));
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(new PublicKey(key), false));
        req.CertificateExtensions.Add(new X509AuthorityKeyIdentifierExtension(aid, false));
        req.CertificateExtensions.Add(sans.Build());
        using var pub = req.Create(ca, now, now + validity, Guid.NewGuid().ToByteArray());
        using var pfx = pub.CopyWithPrivateKey(key);
        var cert = pfx.CopyWithKeyStorageFlags(X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        _store.Add(cert);
        _logger.LogInformation("Issued TLS certificate: {Subject}", subject.Name);
        return cert;
    }

    private X509Certificate2 IssueAuthorityCertificate()
    {
        var cn = Environment.MachineName.Replace('-', ' ').Capitalize(CultureInfo.CurrentCulture);
        var now = DateTime.Now;
        var subject = new X500DistinguishedName($"CN={cn} Root CA");
        using var key = RSA.Create(2048);
        var req = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.KeyCertSign, true));
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(new PublicKey(key), false));
        using var pfx = req.CreateSelfSigned(now, now + CAValidity);
        var cert = pfx.CopyWithKeyStorageFlags(X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        _store.Add(cert);
        _logger.LogInformation("Issued CA certificate: {Subject}", subject.Name);
        return cert;
    }
}
