internal static class HostsServiceCollectionExtensions
{
    internal static IServiceCollection AddHosts(this IServiceCollection services, IConfiguration configuration) => services
        .AddOptions()
        .AddSingleton<IOptionsChangeTokenSource<Hosts>>(new ConfigurationChangeTokenSource<Hosts>(configuration))
        .AddSingleton<IConfigureOptions<Hosts>>(new HostsConfigureOptions(configuration));
}
