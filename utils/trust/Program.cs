if (args.Contains("-h") || args.Contains("--help"))
{
    Console.Error.WriteLine("Usage: triode-trust <hostname>");
    return;
}
var hostname = args.FirstOrDefault("localhost");

using var client = new HttpClient();
try
{
    _ = await client.GetStringAsync($"https://{hostname}/robots.txt").ConfigureAwait(false);
}
catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
{
    var pem = await client.GetStringAsync($"http://{hostname}/root.crt").ConfigureAwait(false);
    using var cert = X509Certificate2.CreateFromPem(pem);
    CertificateManager.Instance.TrustCertificate(cert);
}
catch (Exception ex)
{
    while (ex.InnerException is not null)
        ex = ex.InnerException;
    Console.Error.WriteLine("Error: {0}", ex.Message);
}
