internal sealed class HostsConfigureOptions : IConfigureOptions<Hosts>
{
    private readonly IConfiguration _configuration;

    public HostsConfigureOptions(IConfiguration configuration) => _configuration = configuration;

    public void Configure(Hosts options)
    {
        var lines = _configuration.GetSection(nameof(Hosts)).AsEnumerable(true)
            .OrderBy(p => int.Parse(p.Key, NumberStyles.None, CultureInfo.InvariantCulture))
            .Select(p => p.Value);
        options.Configure(lines);
    }
}
