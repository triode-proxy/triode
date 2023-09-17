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
        _hostname = Dns.GetHostName().ToLowerInvariant();
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

    private async void Answer(UdpClient server, DnsPacket query, IPEndPoint remote, CancellationToken stopping)
    {
        var time = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (query.Type != DnsPacketType.Query || query.Header.QuestionCount != 1)
            {
                var error = DnsPacket.CreateResponse(query.Header, DnsResponseCode.FormatError);
                await server.SendAsync(error.Memory, remote, stopping).ConfigureAwait(false);
                return;
            }
            var question = query.Questions.Single();
            var name = question.Name.ToString();
            var type = question.Type;
            if (type is not DnsRecordType.A and not DnsRecordType.AAAA and not DnsRecordType.ANY)
            {
                var response = await _resolver.SendAsync(query, stopping).ConfigureAwait(false);
                await server.SendAsync(response.Memory, remote, stopping).ConfigureAwait(false);
            }
            else if (!_settings.CurrentValue.Rules.TryGetValue(name, out var behavior) || behavior == Behavior.Pass)
            {
                var (addrs, ttl, code) = await _resolver.ResolveAsync(name, type, remote.AddressFamily, stopping).ConfigureAwait(false);
                var response = addrs.Count > 0
                    ? DnsPacket.CreateResponse(question, ttl, addrs)
                    : DnsPacket.CreateResponse(query.Header, code);
                await server.SendAsync(response.Memory, remote, stopping).ConfigureAwait(false);
            }
            else if (behavior is Behavior.Proxy or Behavior.Secure)
            {
                var addrs = await GetServerAddressesAsync(type, remote, stopping).ConfigureAwait(false);
                var response = DnsPacket.CreateResponse(question, _settings.CurrentValue.TTL.Positive, addrs);
                await server.SendAsync(response.Memory, remote, stopping).ConfigureAwait(false);
                _records.Send(new(
                    $"{query.Header.Id:X4}",
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
                var response = DnsPacket.CreateResponse(query.Header, DnsResponseCode.Refused);
                await server.SendAsync(response.Memory, remote, stopping).ConfigureAwait(false);
                _records.Send(new(
                    $"{query.Header.Id:X4}",
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
            var response = DnsPacket.CreateResponse(query.Header, DnsResponseCode.ServerFailure);
            await server.SendAsync(response.Memory, remote, stopping).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyCollection<IPAddress>> GetServerAddressesAsync(DnsRecordType type, IPEndPoint remote, CancellationToken stopping)
    {
        if (IPAddress.IsLoopback(remote.Address))
            return _networks.Select(n => n.Prefix).Where(a => a.AddressFamily == remote.AddressFamily && IPAddress.IsLoopback(a)).ToArray();

        var siblings = _networks.Where(n => n.Prefix.AddressFamily == remote.AddressFamily && n.Contains(remote.Address)).Select(n => n.Prefix);
        if (siblings.Any())
            return siblings.ToArray();

        return (await _resolver.ResolveAsync(_hostname, type, remote.AddressFamily, stopping).ConfigureAwait(false)).Addresses;
    }
}
