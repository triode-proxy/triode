internal static class HostsConfigurationExtensions
{
    private static string SysConfDir => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetEnvironmentVariable("SystemRoot")!, "System32", "drivers", "etc")
        : "/etc";

    internal static IConfigurationBuilder AddHostsFile(this IConfigurationBuilder builder, bool optional = true, bool reloadOnChange = true) =>
        builder.AddHostsFile(source =>
        {
            var filters = ExclusionFilters.Sensitive;
            var provider = new PhysicalFileProvider(SysConfDir, filters);
            source.FileProvider = provider;
            source.Path = "hosts";
            source.Optional = optional;
            source.ReloadOnChange = reloadOnChange;

            // WORKAROUND: do not watch subdirectories to avoid UnauthorizedAccessException while accessing /etc/**/*
            var underlying = new FileSystemWatcher(provider.Root, source.Path);
            var watcher = new PhysicalFilesWatcher(provider.Root, underlying, provider.UsePollingFileWatcher, filters);
            underlying.IncludeSubdirectories = false;
            typeof(PhysicalFileProvider).GetProperty("FileWatcher", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(provider, watcher);
        });

    internal static IConfigurationBuilder AddHostsFile(this IConfigurationBuilder builder, Action<HostsConfigurationSource> configureSource) =>
        builder.Add(configureSource);
}
