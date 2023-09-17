Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var web = WebApplication.CreateBuilder(args);
web.Configuration.AddHostsFile();
web.Services.AddHosts(web.Configuration);
web.Services.Configure<Settings>(web.Configuration);
web.Services.Configure<Settings>(settings =>
{
    settings.Rules = web.Configuration.GetSection(nameof(settings.Rules)).AsEnumerable(true)
        .ToDictionary(p => new Wildcard(p.Key, IgnoreCase | Compiled | CultureInvariant), p => Enum.Parse<Behavior>(p.Value, true));
});
web.Services.AddHttpClient(Options.DefaultName)
    .SetHandlerLifetime(web.Configuration.Get<Settings>().Upstream.Http.Handler)
    .ConfigureHttpClient((services, client) => client.Timeout = services.GetRequiredService<IOptionsMonitor<Settings>>().CurrentValue.Upstream.Http.Timeout)
    .ConfigureHttpMessageHandlerBuilder(builder =>
    {
        var resolver = builder.Services.GetRequiredService<Resolver>();
        builder.PrimaryHandler = new SocketsHttpHandler
        {
            ConnectCallback = async (connection, canceled) =>
            {
                var (addrs, _, code) = await resolver.ResolveAsync(connection.DnsEndPoint.Host, canceled).ConfigureAwait(false);
                if (addrs.Count == 0)
                    throw new InvalidOperationException("Name not resolved");
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(addrs.First(), connection.DnsEndPoint.Port, canceled).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            UseProxy = false,
        };
    });
web.Services.AddMemoryCache(cache => cache.SizeLimit = Math.Min(uint.MaxValue, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 2));
web.Services.AddResponseCompression(compression => compression.EnableForHttps = true);
web.Services.AddSingleton<PubSub<Record>>();
web.Services.AddSingleton<Authority>();
web.Services.AddSingleton<Resolver>();
web.Services.AddHostedService<Service>();
web.WebHost.ConfigureKestrel(kestrel =>
{
    var memcache = kestrel.ApplicationServices.GetRequiredService<IMemoryCache>();
    var authority = kestrel.ApplicationServices.GetRequiredService<Authority>();
    kestrel.AddServerHeader = false;
    kestrel.ConfigureHttpsDefaults(https =>
    {
        https.ServerCertificateSelector = (connection, name) =>
        {
            if (name is not { Length: > 0 })
                name = (connection?.LocalEndPoint as IPEndPoint)?.Address?.ToString();
            if (name is null)
                return null;
            return memcache.GetOrCreate<X509Certificate2>((nameof(X509Certificate2), name), entry =>
            {
                var cert = authority.GetServerCertificate(name);
                entry.SetOptions(new MemoryCacheEntryOptions()
                    .DisposeOnEvicted()
                    .SetAbsoluteExpiration(cert.NotAfter)
                    .SetSize(Unsafe.SizeOf<X509Certificate2>()));
                return cert;
            });
        };
    });
});
web.WebHost.UseUrls("http://0.0.0.0", "https://0.0.0.0", "http://[::]", "https://[::]");

var app = web.Build();
app.UseWebSockets();
app.UseMiddleware<Middleware>();
app.Run();
