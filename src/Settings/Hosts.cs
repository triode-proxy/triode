internal sealed class Hosts
{
    private ILookup<string, IPAddress>? _hosts;

    public void Configure(IEnumerable<string> lines) =>
        Interlocked.Exchange(ref _hosts, Parse(lines).ToLookup(e => e.Host, e => e.Address, StringComparer.OrdinalIgnoreCase));

    public IEnumerable<IPAddress> GetAddresses(string host) => _hosts?[host] ?? Array.Empty<IPAddress>();

    private static IEnumerable<(string Host, IPAddress Address)> Parse(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var comment = line.IndexOf('#');
            var columns = (comment < 0 ? line : line[..comment])
                .Split(new[] { ' ', '\t' }, RemoveEmptyEntries | TrimEntries);
            if (columns.Length < 2 || !IPAddress.TryParse(columns[0], out var address))
                continue;
            foreach (var host in columns.Skip(1))
                yield return (host, address);
        }
    }
}
