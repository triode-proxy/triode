internal sealed class Middleware
{
    /// <summary>
    /// <see cref="Microsoft.AspNetCore.Mvc.Infrastructure.FileResultExecutorBase.BufferSize" />
    /// </summary>
    private const int BufferSize = 64 * 1024;

    private const int Status499Aborted = 499;

    private const string Gzip = "gzip";
    private const string Deflate = "deflate";
    private const string Brotli = "br";

    private static readonly string ResourceCacheControl = new CacheControlHeaderValue { Public = true, MaxAge = TimeSpan.FromDays(1) }.ToString();
    private static readonly PathString ResolvConfPath = new("/resolv.conf");
    private static readonly Regex UriSchemeHttpPattern = new("^http(?=:)", Compiled | CultureInvariant);
    private static readonly Regex UriSchemeHttpsPattern = new("^https(?=:)", Compiled | CultureInvariant);
    private static readonly Regex CommonNamePattern = new(@"(?<=\bCN=)([^,]+)", Compiled | CultureInvariant);
    private static readonly Regex UnsafeCharPattern = new(@"[\s""#*/:<>?\\|]+", Compiled | CultureInvariant);
    private static readonly Wildcard CertificatePattern = new("*.crt", IgnoreCase | Compiled | CultureInvariant);
    private static readonly Wildcard CrlPattern = new("*.crl", IgnoreCase | Compiled | CultureInvariant);

    private static readonly object BodyBytesSentKey = new();
    private static readonly long MaxDetailsCacheSize = Math.Min(MemoryPool<byte>.Shared.MaxBufferSize, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 8);
    private static readonly TimeSpan DetailsCacheDuration = TimeSpan.FromDays(1);

    private static readonly IReadOnlyCollection<string> Http2PseudoHeaders = new HashSet<string>(new[]
    {
        HeaderNames.Authority,
        HeaderNames.Method,
        HeaderNames.Path,
        HeaderNames.Scheme,
        HeaderNames.Status,
    });

    private static readonly IReadOnlyCollection<string> NonPropagatableHeaders = new[]
    {
        HeaderNames.Connection,
        HeaderNames.TE,
        HeaderNames.Trailer,
        HeaderNames.TransferEncoding,
    };

    private static readonly IReadOnlyCollection<IPNetwork> PrivateNetworks = new IPNetwork[]
    {
        new(IPAddress.Parse("10.0.0.0"), 8),
        new(IPAddress.Parse("172.16.0.0"), 12),
        new(IPAddress.Parse("192.168.0.0"), 16),
    };

    private static readonly ReadOnlyMemory<byte> SecWebSocketAcceptSalt = Encoding.ASCII.GetBytes("258EAFA5-E914-47DA-95CA-C5AB0DC85B11");

    private static readonly RandomNumberGenerator RNG = RandomNumberGenerator.Create();

    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger _logger;
    private readonly IHttpClientFactory _clients;
    private readonly IMemoryCache _memcache;
    private readonly IOptionsMonitor<Settings> _settings;
    private readonly PubSub<Record> _records;
    private readonly Authority _authority;
    private readonly Resolver _resolver;

    private readonly IReadOnlyCollection<string> _hostnames;
    private readonly IReadOnlyCollection<IPAddress> _addresses;
    private readonly IReadOnlyCollection<IPNetwork> _intranets;
    private readonly ResponseCompressionMiddleware _resource;

    private sealed class Details : IDisposable
    {
        public IMemoryOwner<byte>? RequestContent { get; set; }
        public long? RequestContentLength { get; set; }
        public string? RequestContentType { get; set; }
        public IMemoryOwner<byte>? ResponseContent { get; set; }
        public string? ResponseContentEncoding { get; set; }
        public long? ResponseContentLength { get; set; }
        public string? ResponseContentType { get; set; }

        public long Size => SizeOf(RequestContent) + SizeOf(ResponseContent);

        public void Dispose()
        {
            RequestContent?.Dispose();
            ResponseContent?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public Middleware(RequestDelegate next,
        IWebHostEnvironment env,
        IHostApplicationLifetime lifetime,
        ILoggerFactory loggers,
        IHttpClientFactory clients,
        IMemoryCache memcache,
        IResponseCompressionProvider provider,
        IOptionsMonitor<Settings> settings,
        PubSub<Record> records,
        Authority authority,
        Resolver resolver)
    {
        _lifetime = lifetime;
        _logger = loggers.CreateLogger<Middleware>();
        _clients = clients;
        _memcache = memcache;
        _settings = settings;
        _records = records;
        _authority = authority;
        _resolver = resolver;

        var hostname = Dns.GetHostName().ToLowerInvariant();
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                      && ni.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
            .Where(uni => !uni.Address.IsIPv6LinkLocal)
            .ToArray();
        _addresses = addresses.Select(uni => uni.Address).ToArray();
        _intranets = addresses.Where(uni => uni.Address.IsIPv6UniqueLocal || PrivateNetworks.Any(n => n.Contains(uni.Address)))
                              .Select(uni => new IPNetwork(uni.Address, uni.PrefixLength))
                              .ToArray();
        _hostnames = hostname.Contains('.') || _intranets.Count == 0
            ? new[] { hostname }
            : new[] { hostname, $"{hostname}.local" };
        _authority.CrlDistributionPoints.AddRange(
            _hostnames.Select(hostname => new UriBuilder(Uri.UriSchemeHttp, hostname) { Path = "/root.crl" }.Uri)
            );

        var resource = new StaticFileMiddleware(next, env,
            Options.Create(new StaticFileOptions().WithCacheControl(env.IsDevelopment()
                ? CacheControlHeaderValue.NoStoreString
                : ResourceCacheControl)),
            loggers);
        var defaults = new DefaultFilesMiddleware(context => resource.Invoke(context), env, Options.Create(new DefaultFilesOptions()));
        _resource = new ResponseCompressionMiddleware(context => defaults.Invoke(context), provider);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var (stopwatch, time) = (Stopwatch.StartNew(), DateTimeOffset.Now);
        var (aborted, stopping) = (context.RequestAborted, _lifetime.ApplicationStopping);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(aborted, stopping);
        try
        {
            var addrs = await GetAddressesAsync(context.Request.Host.Host, linked.Token).ConfigureAwait(false);
            if (addrs.Any(IPAddress.IsLoopback) || addrs.Intersect(_addresses).Any())
            {
                context.Response.Headers.Server = context.Request.Host.Host;
                if (context.Connection.RemoteIpAddress is not IPAddress remote)
                    throw new BadHttpRequestException("Invalid remote address", Status400BadRequest);
                if (context.WebSockets.IsWebSocketRequest)
                {
                    using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                    await ServeLogRecordsAsync(remote, socket, linked.Token).ConfigureAwait(false);
                }
                else if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                {
                    await ServeLocalFilesAsync(remote, context, linked.Token).ConfigureAwait(false);
                }
                else
                {
                    context.Response.End(Status405MethodNotAllowed);
                }
                return;
            }

            if (!_settings.CurrentValue.Rules.TryGetValue(context.Request.Host.Host, out var behavior) ||
                behavior is not Behavior.Proxy and not Behavior.Secure)
            {
                _logger.LogError("Unexpected request for {Behavior} {Host}", behavior, context.Request.Host);
                context.Response.End(Status404NotFound);
                return;
            }

            if (context.WebSockets.IsWebSocketRequest)
            {
                await ProxyWebSocketMessagesAsync(context, linked.Token).ConfigureAwait(false);
                return;
            }

            await ProxyHttpRequestsAsync(context, behavior, linked.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            _logger.LogError("{Message}", ex.Message);
            context.Response.End(Status400BadRequest);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            context.Response.End(ex.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            context.Response.End(Status502BadGateway);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogDebug("WebSocket closed prematurely");
            context.Response.End(Status502BadGateway);
        }
        catch (OperationCanceledException) when (aborted.IsCancellationRequested)
        {
            _logger.LogDebug("Request aborted from {Remote}", context.Connection.RemoteIpAddress);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            _logger.LogDebug("Application stopping");
            context.Response.End(Status503ServiceUnavailable);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            _logger.LogError("Timeout exceeded while proxying {Uri}", context.Request.GetRawUri());
            context.Response.End(Status504GatewayTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.End(Status500InternalServerError);
        }
        if (!context.WebSockets.IsWebSocketRequest)
        {
            _records.Send(new(
                context.TraceIdentifier,
                time.ToUnixTimeMilliseconds(),
                context.Connection.RemoteIpAddress,
                context.Request.Method,
                context.Request.GetRawUri(),
                context.Request.Protocol,
                aborted.IsCancellationRequested ? Status499Aborted : context.Response.StatusCode,
                aborted.IsCancellationRequested ? "Aborted" : context.Features.Get<IHttpResponseFeature>()?.ReasonPhrase,
                context.Items.TryGetValue(BodyBytesSentKey, out var sent) ? (long)sent! : default,
                stopwatch.ElapsedMilliseconds,
                context.Request.Headers.ExceptBy(Http2PseudoHeaders, p => p.Key).ToArray(),
                context.Response.Headers.ExceptBy(Http2PseudoHeaders, p => p.Key).ToArray()
                ));
        }
    }

    private async Task ServeLogRecordsAsync(IPAddress remote, WebSocket socket, CancellationToken aborted)
    {
        var local = IPAddress.IsLoopback(remote);
        var intra = _intranets.FirstOrDefault(n => n.Contains(remote));
        using var subscriber = _records.Subscribe();
        while (socket.State == WebSocketState.Open && !aborted.IsCancellationRequested)
        {
            var record = await subscriber.ReceiveAsync(aborted).ConfigureAwait(false);
            if (_settings.CurrentValue.Promiscuous || local || remote.Equals(record.From) || record.From is not null && intra?.Contains(record.From) == true)
                await socket.SendAsync(record.Memory, WebSocketMessageType.Text, true, aborted).ConfigureAwait(false);
            else
                _logger.LogTrace("Skipped serving log from {From} to {Remote}", record.From, remote);
        }
    }

    private async Task ServeLocalFilesAsync(IPAddress remote, HttpContext context, CancellationToken aborted)
    {
        if (context.Request.Query.Any())
        {
            var (q, r) = (context.Request.Query["q"], context.Request.Query["r"]);
            if (_memcache.TryGetValue<Details>($"{q}{r}", out var details))
            {
                if (q.Count > 0 && details.RequestContent is not null)
                {
                    if (details.RequestContent.Memory.Length == 0)
                    {
                        context.Response.StatusCode = Status204NoContent;
                    }
                    else if (details.RequestContentLength is long length && details.RequestContent.Memory.Length < length)
                    {
                        context.Response.StatusCode = Status206PartialContent;
                        context.Response.GetTypedHeaders().ContentRange = new(0, details.RequestContent.Memory.Length - 1, length);
                    }
                    context.Response.ContentLength = details.RequestContent.Memory.Length;
                    if (details.RequestContentType is { Length: > 0 })
                        context.Response.ContentType = details.RequestContentType;
                    if (!HttpMethods.IsHead(context.Request.Method) && details.RequestContent.Memory.Length > 0)
                    {
                        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
                        await context.Response.StartAsync(aborted).ConfigureAwait(false);
                        await context.Response.Body.WriteAsync(details.RequestContent.Memory, aborted).ConfigureAwait(false);
                    }
                    return;
                }
                if (r.Count > 0 && details.ResponseContent is not null)
                {
                    if (details.ResponseContent.Memory.Length == 0)
                    {
                        context.Response.StatusCode = Status204NoContent;
                    }
                    else if (details.ResponseContentLength is long length && details.ResponseContent.Memory.Length < length)
                    {
                        context.Response.StatusCode = Status206PartialContent;
                        context.Response.GetTypedHeaders().ContentRange = new(0, details.ResponseContent.Memory.Length - 1, length);
                    }
                    context.Response.ContentLength = details.ResponseContent.Memory.Length;
                    if (details.ResponseContentEncoding is { Length: > 0 })
                        context.Response.Headers.ContentEncoding = details.ResponseContentEncoding;
                    if (details.ResponseContentType is { Length: > 0 })
                        context.Response.ContentType = details.ResponseContentType;
                    if (!HttpMethods.IsHead(context.Request.Method) && details.ResponseContent.Memory.Length > 0)
                    {
                        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
                        await context.Response.StartAsync(aborted).ConfigureAwait(false);
                        await context.Response.Body.WriteAsync(details.ResponseContent.Memory, aborted).ConfigureAwait(false);
                    }
                    return;
                }
            }
            context.Response.End(Status404NotFound);
            return;
        }
        if (CertificatePattern.IsMatch(context.Request.Path))
        {
            var cert = _authority.Certificate;
            var data = Encoding.ASCII.GetBytes(PemEncoding.Write("CERTIFICATE", cert.RawData));
            var name = UnsafeCharPattern.Replace(CommonNamePattern.Match(cert.Subject).Value, "_");
            context.Response.ContentLength = data.Length;
            context.Response.ContentType = "application/x-pem-file";
            context.Response.GetTypedHeaders().ContentDisposition = new("attachment") { FileName = $"{name}.crt" };
            if (!HttpMethods.IsHead(context.Request.Method))
                await context.Response.Body.WriteAsync(data, aborted).ConfigureAwait(false);
            return;
        }
        if (CrlPattern.IsMatch(context.Request.Path))
        {
            var data = _authority.CertificateRevocationList;
            context.Response.ContentLength = data.Length;
            context.Response.ContentType = "application/pkix-crl";
            if (!HttpMethods.IsHead(context.Request.Method))
                await context.Response.Body.WriteAsync(data, aborted).ConfigureAwait(false);
            return;
        }
        if (context.Request.Path == ResolvConfPath)
        {
            var name = context.Request.Host.Host;
            var addrs = IPAddress.TryParse(name, out var addr) ? new[] { addr }
                : (await _resolver.ResolveAsync(name, DnsRecordType.A, remote.AddressFamily, aborted).ConfigureAwait(false)).Addresses;
            var data = Encoding.ASCII.GetBytes(string.Join(string.Empty, addrs.Select(a => $"nameserver {a}\n")));
            context.Response.ContentLength = data.Length;
            context.Response.ContentType = "text/plain";
            context.Response.GetTypedHeaders().ContentDisposition = new("attachment") { FileName = "resolv.conf" };
            if (!HttpMethods.IsHead(context.Request.Method))
                await context.Response.Body.WriteAsync(data, aborted).ConfigureAwait(false);
            return;
        }
        await _resource.Invoke(context).ConfigureAwait(false);
    }

    private async Task ProxyWebSocketMessagesAsync(HttpContext context, CancellationToken aborted)
    {
        var (stopwatch, time) = (Stopwatch.StartNew(), DateTimeOffset.Now);
        var upgrader = context.Features.Get<IHttpUpgradeFeature>();
        if (upgrader?.IsUpgradableRequest != true)
            throw new ArgumentException("Request not upgradable", nameof(context));
        if (!context.Request.Headers.TryGetValue(HeaderNames.SecWebSocketKey, out var key))
            throw new ArgumentException($"{HeaderNames.SecWebSocketKey} header is missing", nameof(context));
        using var client = _clients.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri.CopyFrom(context.Request));
        foreach (var (name, values) in context.Request.Headers
            .ExceptBy(Http2PseudoHeaders, p => p.Key, StringComparer.Ordinal)
            .ExceptBy(NonPropagatableHeaders.Except(HeaderNames.Connection), p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (name.Equals(HeaderNames.Cookie, OrdinalIgnoreCase))
                request.Headers.TryAddWithoutValidation(name, string.Join("; ", values));
            else
                request.Headers.TryAddWithoutValidation(name, values.AsEnumerable());
        }
        client.Timeout = Timeout.InfiniteTimeSpan;
        using var response = await client.SendAsync(request, ResponseHeadersRead, aborted).ConfigureAwait(false);
        if (response.StatusCode != SwitchingProtocols)
            throw new WebSocketException(WebSocketError.NotAWebSocket, $"The server returned status code '{(int)response.StatusCode}' when status code '101' was expected.");
        if (!response.Headers.TryGetValues(HeaderNames.SecWebSocketAccept, out var accept) || ComputeSecWebSockeAccept(key) != accept.Single())
            throw new WebSocketException(WebSocketError.HeaderError, $"The '{HeaderNames.SecWebSocketAccept}' header value is missing or invalid.");
        if (response.Content is null)
            throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
        context.Response.StatusCode = Status101SwitchingProtocols;
        context.Features.Get<IHttpResponseFeature>()!.ReasonPhrase = response.ReasonPhrase;
        foreach (var (name, values) in response.Headers
            .ExceptBy(NonPropagatableHeaders.Except(HeaderNames.Connection), p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (name.Equals(HeaderNames.Server, OrdinalIgnoreCase))
                context.Response.Headers.Server = string.Join(' ', values);
            else
                context.Response.Headers.Append(name, values.ToArray());
        }
        // TODO: add deflate, subprotocols support
        using var downstream = await upgrader.UpgradeAsync().ConfigureAwait(false);
        using var upstream = await response.Content.ReadAsStreamAsync(aborted).ConfigureAwait(false);
        _records.Send(new(
            context.TraceIdentifier,
            time.ToUnixTimeMilliseconds(),
            context.Connection.RemoteIpAddress,
            context.Request.Method,
            context.Request.GetRawUri(),
            context.Request.Protocol,
            context.Response.StatusCode,
            context.Features.Get<IHttpResponseFeature>()?.ReasonPhrase,
            default,
            stopwatch.ElapsedMilliseconds,
            context.Request.Headers.ExceptBy(Http2PseudoHeaders, p => p.Key).ToArray(),
            context.Response.Headers.ExceptBy(Http2PseudoHeaders, p => p.Key).ToArray()
            ));
        var (id, from) = (context.TraceIdentifier, context.Connection.RemoteIpAddress);
        try
        {
            await Task.WhenAll(
                ProxyWebSocketFramesAsync(id, from, downstream, upstream, aborted),
                ProxyWebSocketFramesAsync(id, from, upstream, downstream, aborted)
                ).ConfigureAwait(false);
        }
        catch (EndOfStreamException ex)
        {
            throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely, ex);
        }
    }

    private async Task ProxyWebSocketFramesAsync(string id, IPAddress? from, Stream source, Stream target, CancellationToken aborted)
    {
        using var buffer = MemoryPool<byte>.Shared.Rent(BufferSize);
        var memory = buffer.Memory;
        while (!aborted.IsCancellationRequested)
        {
            await source.ReadExactlyAsync(memory[..2], aborted).ConfigureAwait(false);
            await target.WriteAsync(memory[..2], aborted).ConfigureAwait(false);
            var final  = (memory.Span[0] & 0x80) != 0;
            var opcode = (memory.Span[0] & 0x0F);
            var masked = (memory.Span[1] & 0x80) != 0;
            var length = (memory.Span[1] & 0x7F) + 0L;
            if (length == 126)
            {
                await source.ReadExactlyAsync(memory[..2], aborted).ConfigureAwait(false);
                length = ReadUInt16BigEndian(memory.Span[..2]);
                Debug.Assert(length >= 126);
                await target.WriteAsync(memory[..2], aborted).ConfigureAwait(false);
            }
            else if (length == 127)
            {
                await source.ReadExactlyAsync(memory[..8], aborted).ConfigureAwait(false);
                length = ReadInt64BigEndian(memory.Span[..8]);
                Debug.Assert(length > ushort.MaxValue);
                await target.WriteAsync(memory[..8], aborted).ConfigureAwait(false);
            }
            if (masked)
            {
                await source.ReadExactlyAsync(memory[..4], aborted).ConfigureAwait(false);
                await target.WriteAsync(memory[..4], aborted).ConfigureAwait(false);
            }
            for (long offset = 0; offset < length; )
            {
                var n = (int)Math.Min(memory.Length, length - offset);
                await source.ReadExactlyAsync(memory[..n], aborted).ConfigureAwait(false);
                await target.WriteAsync(memory[..n], aborted).ConfigureAwait(false);
                offset += n;
            }
            _records.Send(new(
                id,
                DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                from,
                string.Empty,
                string.Empty,
                string.Empty,
                opcode,
                null,
                length * (masked ? -1 : +1),
                default,
                Array.Empty<KeyValuePair<string, StringValues>>(),
                Array.Empty<KeyValuePair<string, StringValues>>()
                ));
            if (opcode == 0x8)
                break;
        }
    }

    private async Task ProxyHttpRequestsAsync(HttpContext context, Behavior behavior, CancellationToken aborted)
    {
        long bodyBytesSent = 0;
        #pragma warning disable CA2000
        var details = new Details();
        #pragma warning restore CA2000
        try
        {
            var rules = _settings.CurrentValue.Rules;
            var securing = context.Request.Scheme == Uri.UriSchemeHttp && behavior == Behavior.Secure;
            var requestUri = RequestUri.Create(
                securing ? Uri.UriSchemeHttps : context.Request.Scheme,
                context.Request.Host.Host,
                context.Request.Host.Port,
                context.Features.Get<IHttpRequestFeature>()!.RawTarget);
            using var client = _clients.CreateClient();
            using var request = new HttpRequestMessage(new(context.Request.Method), requestUri);
            if (!HttpMethods.IsGet(context.Request.Method) &&
                !HttpMethods.IsHead(context.Request.Method) &&
                !HttpMethods.IsOptions(context.Request.Method))
            {
                var requestContent = new MemoryPoolStream((int)(context.Request.ContentLength ?? 0));
                details.RequestContent = requestContent;
                details.RequestContentLength = context.Request.ContentLength;
                details.RequestContentType = context.Request.ContentType;
                var contentType = context.Request.GetTypedHeaders().ContentType;
                if (contentType?.MediaType.HasValue == true &&
                    _settings.CurrentValue.Subs?.TryGetValue(contentType.MediaType.Value, out var subs) == true &&
                    contentType.TryGetEncoding(out var charset))
                {
                    await context.Request.Body.CopyToAsync(requestContent, aborted).ConfigureAwait(false);
                    var text = charset.GetString(requestContent.Memory.Span);
                    foreach (var (from, to) in subs)
                        text = text.Replace(from, to);
                    request.Content = new StringContent(text, charset, contentType.MediaType.Value);
                }
                else
                {
                    request.Content = new StreamSplitContent(context.Request.Body, requestContent, MaxDetailsCacheSize, aborted);
                    context.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize = null;
                }
            }
            bool ShouldBeSecure(string? value)
                => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp
                && rules.TryGetValue(uri.Host, out var behavior) && behavior == Behavior.Secure;
            foreach (var (name, values) in context.Request.Headers
                .ExceptBy(Http2PseudoHeaders, p => p.Key)
                .ExceptBy(NonPropagatableHeaders, p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (name.StartsWith("Content-", OrdinalIgnoreCase))
                    request.Content?.Headers?.TryAddIfNotPresent(name, values);
                else if (name.Equals(HeaderNames.Cookie, OrdinalIgnoreCase))
                    request.Headers.TryAddWithoutValidation(name, string.Join("; ", values));
                else if (securing && name.Equals(HeaderNames.Origin, OrdinalIgnoreCase) && ShouldBeSecure(values))
                    request.Headers.TryAddWithoutValidation(name, UriSchemeHttpPattern.Replace(values, Uri.UriSchemeHttps));
                else if (securing && name.Equals(HeaderNames.Referer, OrdinalIgnoreCase) && ShouldBeSecure(values))
                    request.Headers.TryAddWithoutValidation(name, UriSchemeHttpPattern.Replace(values, Uri.UriSchemeHttps));
                else
                    request.Headers.TryAddWithoutValidation(name, values.AsEnumerable());
            }
            using var response = await client.SendAsync(request, ResponseHeadersRead, aborted).ConfigureAwait(false);
            context.Response.StatusCode = (int)response.StatusCode;
            context.Features.Get<IHttpResponseFeature>()!.ReasonPhrase = response.ReasonPhrase;
            bool ShouldBeInsecure(string? value)
                => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
                && rules.TryGetValue(uri.Host, out var behavior) && behavior == Behavior.Secure;
            foreach (var (name, values) in response.Headers.Concat(response.Content.Headers)
                .ExceptBy(NonPropagatableHeaders, p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (name.Equals(HeaderNames.Server, OrdinalIgnoreCase))
                    context.Response.Headers.Server = string.Join(' ', values);
                else if (securing && name.Equals(HeaderNames.Location, OrdinalIgnoreCase) && ShouldBeInsecure(values.SingleOrDefault()))
                    context.Response.Headers.Location = UriSchemeHttpsPattern.Replace($"{response.Headers.Location}", Uri.UriSchemeHttp);
                else if (securing && name.Equals(HeaderNames.SetCookie, OrdinalIgnoreCase))
                    context.Response.Headers.Append(name, values.Select(RemoveSecureFromSetCookie).ToArray());
                else if (!securing || !name.Equals(HeaderNames.StrictTransportSecurity, OrdinalIgnoreCase))
                    context.Response.Headers.Append(name, values.ToArray());
            }
            if (!HttpMethods.IsHead(context.Request.Method) &&
                OK <= response.StatusCode && response.StatusCode is not NoContent and not NotModified)
            {
                var responseContent = new MemoryPoolStream((int)(response.Content.Headers.ContentLength ?? 0));
                details.ResponseContent = responseContent;
                details.ResponseContentEncoding = string.Join(", ", response.Content.Headers.ContentEncoding);
                details.ResponseContentLength = response.Content.Headers.ContentLength;
                details.ResponseContentType = response.Content.Headers.ContentType?.ToString();
                if (response.Content.Headers.ContentEncoding.Count <= 1 &&
                    response.Content.Headers.ContentEncoding.SingleOrDefault() is Gzip or Deflate or Brotli or null &&
                    response.Content.Headers.ContentType?.MediaType is string mediaType &&
                    _settings.CurrentValue.Subs?.TryGetValue(mediaType, out var subs) == true &&
                    response.Content.Headers.ContentType.TryGetEncoding(out var charset))
                {
                    await response.Content.CopyToAsync(responseContent, aborted).ConfigureAwait(false);
                    if (TryGetContentEncodingFactory(response.Content.Headers.ContentEncoding.SingleOrDefault(), out var contentEncoding))
                    {
                        using var decompressor = contentEncoding.CreateDecompressor(responseContent);
                        using var decompressed = new MemoryPoolStream();
                        await decompressor.CopyToAsync(decompressed, aborted).ConfigureAwait(false);
                        var text = charset.GetString(decompressed.Memory.Span);
                        foreach (var (from, to) in subs)
                            text = text.Replace(from, to);
                        using var compressed = new MemoryPoolStream();
                        using var compressor = contentEncoding.CreateCompressor(compressed);
                        await compressor.WriteAsync(charset.GetBytes(text), aborted).ConfigureAwait(false);
                        await compressor.FlushAsync(aborted).ConfigureAwait(false);
                        var body = compressed.Memory;
                        context.Response.ContentLength = body.Length;
                        context.Response.Headers.Remove(HeaderNames.ContentMD5);
                        await context.Response.Body.WriteAsync(body, aborted).ConfigureAwait(false);
                        bodyBytesSent = body.Length;
                    }
                    else
                    {
                        var text = charset.GetString(responseContent.Memory.Span);
                        foreach (var (from, to) in subs)
                            text = text.Replace(from, to);
                        var body = charset.GetBytes(text);
                        context.Response.ContentLength = body.Length;
                        context.Response.Headers.Remove(HeaderNames.ContentMD5);
                        await context.Response.Body.WriteAsync(body, aborted).ConfigureAwait(false);
                        bodyBytesSent = body.Length;
                    }
                }
                else
                {
                    context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
                    await context.Response.StartAsync(aborted).ConfigureAwait(false);
                    using var stream = await response.Content.ReadAsStreamAsync(aborted).ConfigureAwait(false);
                    using var buffer = MemoryPool<byte>.Shared.Rent(BufferSize);
                    for (int n; (n = await stream.ReadAsync(buffer.Memory, aborted).ConfigureAwait(false)) > 0;)
                    {
                        await context.Response.Body.WriteAsync(buffer.Memory[..n], aborted).ConfigureAwait(false);
                        if (bodyBytesSent < MaxDetailsCacheSize)
                        {
                            var cap = (int)Math.Min(n, MaxDetailsCacheSize - bodyBytesSent);
                            await responseContent.WriteAsync(buffer.Memory[..cap], aborted).ConfigureAwait(false);
                        }
                        bodyBytesSent += n;
                    }
                    await context.Response.CompleteAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (details.Size > 0)
            {
                _memcache.Set(context.TraceIdentifier, details,
                    new MemoryCacheEntryOptions()
                        .DisposeOnEvicted()
                        .SetSize(details.Size)
                        .SetSlidingExpiration(DetailsCacheDuration)
                    );
            }
            else
            {
                details.Dispose();
            }
            context.Items.Add(BodyBytesSentKey, bodyBytesSent);
        }

        static string RemoveSecureFromSetCookie(string value)
        {
            if (SetCookieHeaderValue.TryParse(value, out var cookie))
            {
                cookie.Secure = false;
                return cookie.ToString();
            }
            return value;
        }
    }

    private async Task<IReadOnlyCollection<IPAddress>> GetAddressesAsync(string name, CancellationToken aborted)
    {
        if (name.Equals("localhost", OrdinalIgnoreCase))
            return new[] { IPAddress.Loopback, IPAddress.IPv6Loopback };
        if (_hostnames.Contains(name, StringComparer.OrdinalIgnoreCase))
            return _addresses;
        if (IPAddress.TryParse(name, out var addr))
            return new[] { addr };
        var (addrs, _, _) = await _resolver.ResolveAsync(name, aborted).ConfigureAwait(false);
        return addrs;
    }

    [SuppressMessage("Microsoft.Security", "CA5350", Justification = "Required by RFC6455")]
    private static string ComputeSecWebSockeAccept(string key)
    {
        Span<byte> bytes = stackalloc byte[24 + SecWebSocketAcceptSalt.Length];
        Encoding.ASCII.GetBytes(key, bytes);
        SecWebSocketAcceptSalt.Span.CopyTo(bytes[24..]);
        SHA1.TryHashData(bytes, bytes, out var length);
        return Convert.ToBase64String(bytes[..length]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetContentEncodingFactory(string? name, out (Func<Stream, Stream> CreateCompressor, Func<Stream, Stream> CreateDecompressor) factory)
    {
        switch (name)
        {
            case Gzip:
                factory = (
                    stream => new GZipStream(stream, CompressionLevel.Fastest),
                    stream => new GZipStream(stream, CompressionMode.Decompress, true)
                    );
                return true;
            case Deflate:
                factory = (
                    stream => new DeflateStream(stream, CompressionLevel.Fastest),
                    stream => new DeflateStream(stream, CompressionMode.Decompress, true)
                    );
                return true;
            case Brotli:
                factory = (
                    stream => new BrotliStream(stream, CompressionLevel.Fastest),
                    stream => new BrotliStream(stream, CompressionMode.Decompress, true)
                    );
                return true;
        }
        factory = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SizeOf(IMemoryOwner<byte>? o) => ((o as MemoryPoolStream)?.Capacity ?? o?.Memory.Length ?? 0);
}
