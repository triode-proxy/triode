internal sealed class Service : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IOptionsMonitor<Settings> _settings;
    private readonly PubSub<Record> _records;
    private readonly Resolver _resolver;
    private readonly string _hostname;
    private readonly IReadOnlyCollection<IPNetwork> _networks;

    public Service(ILogger<Service> logger, IOptionsMonitor<Settings> settings, PubSub<Record> records, Resolver resolver)
    {
        _logger = logger;
        _settings = settings;
        _records = records;
        _resolver = resolver;
        _hostname = Dns.GetHostName();
        _networks = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                      && ni.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Loopback or NetworkInterfaceType.Wireless80211)
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
            .Where(uni => !uni.Address.IsIPv6LinkLocal)
            .Select(uni => new IPNetwork(uni.Address, uni.PrefixLength))
            .ToArray();
    }

    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        var endpoints = new[]
        {
            new IPEndPoint(IPAddress.Any, 53),
            new IPEndPoint(IPAddress.IPv6Any, 53),
        };
        try
        {
            await Task.WhenAll(endpoints.Select(ep => ListenAsync(ep, stopping))).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            _logger.LogDebug("Application stopping");
        }
    }

    private async Task ListenAsync(IPEndPoint endpoint, CancellationToken stopping)
    {
        using var server = new UdpClient(endpoint);
        server.DisableConnectionResetReporting();
        while (!stopping.IsCancellationRequested)
        {
            var result = await server.ReceiveAsync(stopping).ConfigureAwait(false);
            Answer(server, new(result.Buffer), result.RemoteEndPoint, stopping);
        }
    }

    private async void Answer(UdpClient server, DnsPacket request, IPEndPoint remote, CancellationToken stopping)
    {
        var time = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (request.Header.QuestionCount != 1)
            {
                var error = DnsPacket.CreateError(request.Header, DnsResponseCode.FormatError);
                await server.SendAsync(error.Memory, remote, stopping).ConfigureAwait(false);
                return;
            }
            var question = request.Questions.Single();
            var name = question.Name.ToString();
            var type = question.Type;
            if (type is not DnsRecordType.A and not DnsRecordType.AAAA)
            {
                var response = await _resolver.SendAsync(request, stopping).ConfigureAwait(false);
                await server.SendAsync(response.Memory, remote, stopping).ConfigureAwait(false);
            }
            else if (!_settings.CurrentValue.Rules.TryGetValue(name, out var behavior) || behavior == Behavior.Pass)
            {
                var (addrs, ttl, code) = await _resolver.ResolveAsync(name, type, stopping).ConfigureAwait(false);
                ttl = TimeSpan.FromSeconds(Math.Min(ttl.TotalSeconds, _settings.CurrentValue.TTL.Proxing.TotalSeconds));
                var response = addrs.Count > 0
                    ? DnsPacket.CreateAnswer(question, type, ttl, addrs)
                    : DnsPacket.CreateError(request.Header, code);
                await server.SendAsync(response.Memory, remote, stopping).ConfigureAwait(false);
            }
            else if (behavior is Behavior.Proxy or Behavior.Secure)
            {
                var addrs = await GetServerAddressesAsync(type, remote, stopping).ConfigureAwait(false);
                var response = DnsPacket.CreateAnswer(question, type, _settings.CurrentValue.TTL.Proxing, addrs);
                await server.SendAsync(response.Memory, remote, stopping).ConfigureAwait(false);
                _records.Send(new(
                    $"{request.Header.Id:X4}",
                    time.ToUnixTimeMilliseconds(),
                    remote.Address,
                    $"{type}",
                    name,
                    "DNS",
                    (int)response.Header.ResponseCode,
                    response.Memory.Length,
                    stopwatch.ElapsedMilliseconds,
                    Array.Empty<KeyValuePair<string, StringValues>>(),
                    Array.Empty<KeyValuePair<string, StringValues>>()
                    ));
            }
            else
            {
                var response = DnsPacket.CreateError(request.Header, DnsResponseCode.Refused);
                await server.SendAsync(response.Memory, remote, stopping).ConfigureAwait(false);
                _records.Send(new(
                    $"{request.Header.Id:X4}",
                    time.ToUnixTimeMilliseconds(),
                    remote.Address,
                    $"{type}",
                    name,
                    "DNS",
                    (int)response.Header.ResponseCode,
                    response.Memory.Length,
                    stopwatch.ElapsedMilliseconds,
                    Array.Empty<KeyValuePair<string, StringValues>>(),
                    Array.Empty<KeyValuePair<string, StringValues>>()
                    ));
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // _logger.LogDebug("Application stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            var response = DnsPacket.CreateError(request.Header, DnsResponseCode.ServerFailure);
            await server.SendAsync(response.Memory, remote, stopping).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyCollection<IPAddress>> GetServerAddressesAsync(DnsRecordType type, IPEndPoint remote, CancellationToken stopping)
    {
        var af = type == DnsRecordType.A ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6;

        if (IPAddress.IsLoopback(remote.Address))
            return _networks.Select(n => n.Prefix).Where(a => a.AddressFamily == af && IPAddress.IsLoopback(a)).ToArray();

        var siblings = _networks.Where(n => n.Prefix.AddressFamily == af && n.Contains(remote.Address)).Select(n => n.Prefix);
        if (siblings.Any())
            return siblings.ToArray();
        if (type == DnsRecordType.AAAA && remote.AddressFamily == AddressFamily.InterNetwork)
            siblings = _networks.Where(n => n.Contains(remote.Address)).Select(n => n.Prefix.MapToIPv6());
        if (siblings.Any())
            return siblings.ToArray();

        return (await _resolver.ResolveAsync(_hostname, stopping).ConfigureAwait(false)).Addresses;
    }
}
