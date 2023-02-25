internal sealed class HostsConfigurationSource : FileConfigurationSource
{
    public override IConfigurationProvider Build(IConfigurationBuilder builder) => new HostsConfigurationProvider(this);
}
