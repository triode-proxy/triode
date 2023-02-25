internal sealed class HostsConfigurationProvider : FileConfigurationProvider
{
    public HostsConfigurationProvider(FileConfigurationSource source) : base(source) { }

    public override void Load(Stream stream) => Data = Read(stream);

    private static IDictionary<string, string> Read(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.GetEncoding(0));
        return reader.ReadLines()
            .Select((value, i) => KeyValuePair.Create(ConfigurationPath.Combine(nameof(Hosts), $"{i}"), value))
            .ToDictionary(p => p.Key, p => p.Value);
    }
}
