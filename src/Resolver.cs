internal sealed class Resolver : IDisposable
{
    private readonly ILogger _logger;
    private readonly IMemoryCache _memcache;
    private readonly IOptionsMonitor<Hosts> _etchosts;
    private readonly IOptionsMonitor<Settings> _settings;

    private readonly bool _v6;
    private readonly UdpClient _client;
    private readonly ConcurrentDictionary<ushort, TaskCompletionSource<UdpReceiveResult>> _completions = new();


    public Resolver(IHostApplicationLifetime lifetime, ILogger<Resolver> logger, IMemoryCache memcache, IOptionsMonitor<Hosts> hosts, IOptionsMonitor<Settings> settings)
    {
        _logger = logger;
        _memcache = memcache;
        _etchosts = hosts;
        _settings = settings;

        _v6 = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                      && ni.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
            .Select(uni => uni.Address)
            .Any(addr => addr.AddressFamily == AddressFamily.InterNetworkV6 && !addr.IsIPv6LinkLocal);
        _client = new(settings.CurrentValue.Upstream.Dns.Address, 53);
        HandleResponses(lifetime.ApplicationStopping);
    }

    public async Task<(IReadOnlyCollection<IPAddress> Addresses, TimeSpan TimeToLive, DnsResponseCode ResponseCode)> ResolveAsync(string name, CancellationToken aborted = default)
    {
        if (_v6)
        {
            var (addrs, ttl, code) = await ResolveAsync(name, DnsRecordType.AAAA, aborted).ConfigureAwait(false);
            if (addrs.Count > 0)
                return (addrs, ttl, code);
        }
        return await ResolveAsync(name, DnsRecordType.A, aborted).ConfigureAwait(false);
    }

    public async Task<(IReadOnlyCollection<IPAddress> Addresses, TimeSpan TimeToLive, DnsResponseCode ResponseCode)> ResolveAsync(string name, DnsRecordType type, CancellationToken aborted = default)
    {
        Debug.Assert(type is DnsRecordType.A or DnsRecordType.AAAA);
        if (_etchosts.CurrentValue.GetAddresses(name) is IEnumerable<IPAddress> addrs && addrs.Any())
        {
            var af = type == DnsRecordType.A ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6;
            return (addrs.Where(a => a.AddressFamily == af).ToArray(), _settings.CurrentValue.TTL.Proxing, DnsResponseCode.NoError);
        }
        return await _memcache.GetOrCreateAsync((name, type), async entry =>
        {
            var now = DateTimeOffset.Now;
            var request = DnsPacket.CreateQuestion(name, type);
            var response = await SendAsync(request, aborted).ConfigureAwait(false);
            var answers = response.Answers.Where(a => a.Type == type).ToArray();
            var addrs = answers.Select(a => new IPAddress(a.Data.Span)).ToArray();
            var ttl = answers.Length > 0
                ? answers.Min(a => a.TimeToLive)
                : response.Header.ResponseCode is DnsResponseCode.NoError or DnsResponseCode.NameError
                    ? _settings.CurrentValue.TTL.Missing
                    : _settings.CurrentValue.TTL.Failure;
            entry.AbsoluteExpiration = now + ttl;
            entry.Size = answers.Sum(a => a.Data.Length);
            return (addrs, ttl, response.Header.ResponseCode);
        }).ConfigureAwait(false);
    }

    public async Task<DnsPacket> SendAsync(DnsPacket request, CancellationToken aborted = default)
    {
        var completion = new TaskCompletionSource<UdpReceiveResult>();
        if (!_completions.TryAdd(request.Header.Id, completion))
        {
            _logger.LogError("DNS packet ID conflicted");
            return DnsPacket.CreateError(request.Header, DnsResponseCode.ServerFailure);
        }
        try
        {
            var timeout = _settings.CurrentValue.Upstream.Dns.Timeout;
            await _client.SendAsync(request.Memory, aborted).ConfigureAwait(false);
            var result = await completion.Task.WithTimeout(timeout, aborted).ConfigureAwait(false);
            var response = new DnsPacket(result.Buffer);
            if (!response.Header.IsResponse)
                return DnsPacket.CreateError(request.Header, DnsResponseCode.ServerFailure);
            if (response.Header.IsTruncated)
                return DnsPacket.CreateError(request.Header, DnsResponseCode.NotImplemented);
            return response;
        }
        catch (TimeoutException)
        {
            _logger.LogError("Timeout exceeded while resolving {Name}", request.Questions.FirstOrDefault().Name);
            return DnsPacket.CreateError(request.Header, DnsResponseCode.ServerFailure);
        }
        finally
        {
            _completions.TryRemove(request.Header.Id, out _);
        }
    }

    private async void HandleResponses(CancellationToken stopping)
    {
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                var result = await _client.ReceiveAsync(stopping).ConfigureAwait(false);
                if (_completions.TryGetValue(new DnsPacket(result.Buffer).Header.Id, out var completion))
                    completion.TrySetResult(result);
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            _logger.LogDebug("Application stopping");
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
