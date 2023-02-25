internal sealed class Hosts
{
    private IReadOnlyCollection<(string Host, IPAddress Address)> _hosts = Array.Empty<(string, IPAddress)>();

    public void Configure(IEnumerable<string> lines) =>
        Interlocked.Exchange(ref _hosts, Parse(lines).ToArray());

    public IEnumerable<IPAddress> GetAddresses(string host) =>
        _hosts.Where(e => e.Host.Equals(host, OrdinalIgnoreCase)).Select(e => e.Address);

    private static IEnumerable<(string Host, IPAddress Address)> Parse(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var comment = line.IndexOf('#');
            var columns = (comment < 0 ? line : line[..comment]).Split(' ', RemoveEmptyEntries | TrimEntries);
            if (columns.Length < 2 || !IPAddress.TryParse(columns[0], out var address))
                continue;
            foreach (var host in columns.Skip(1))
                yield return (host, address);
        }
    }
}
