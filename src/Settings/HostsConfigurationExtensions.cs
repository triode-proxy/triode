internal static class HostsConfigurationExtensions
{
    private static string SysConfDir => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetEnvironmentVariable("SystemRoot")!, "System32", "drivers", "etc")
        : "/etc";

    internal static IConfigurationBuilder AddHostsFile(this IConfigurationBuilder builder, bool optional = true, bool reloadOnChange = true) =>
        builder.AddHostsFile(source =>
        {
            source.FileProvider = new PhysicalFileProvider(SysConfDir, default);
            source.Path = "hosts";
            source.Optional = optional;
            source.ReloadOnChange = reloadOnChange;
        });

    internal static IConfigurationBuilder AddHostsFile(this IConfigurationBuilder builder, Action<HostsConfigurationSource> configureSource) =>
        builder.Add(configureSource);
}
