namespace Microsoft.AspNetCore.Certificates.Generation;

internal sealed class CertificateManager
{
    private static readonly Type _type = typeof(Server.Kestrel.Core.KestrelServerOptions).Assembly.GetType(typeof(CertificateManager).FullName!)!;

    public static CertificateManager Instance { get; } = new(_type.GetProperty(nameof(Instance), BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!);

    private readonly object _instance;

    private CertificateManager(object instance) => _instance = instance;

    internal void TrustCertificate(X509Certificate2 certificate)
    {
        _type.GetMethod(nameof(TrustCertificate), BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_instance, new object?[] { certificate });
    }
}
