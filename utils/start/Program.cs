var progname = Environment.GetCommandLineArgs()[0];
if (args.Contains("-h") || args.Contains("--help"))
{
    Console.Error.WriteLine($"Usage: {progname} <hostname>");
    return;
}
try
{
    var hostname = args.FirstOrDefault("localhost");
    var addresses = Dns.GetHostAddresses(hostname);

    if (OperatingSystem.IsMacOS() && geteuid() != 0)
    {
        const string sudo = "/usr/bin/sudo";
        var argv = new[] { progname }.Concat(args).Concat(new string?[] { null });
        if (progname.EndsWith(".dll", OrdinalIgnoreCase))
            argv = new[] { "dotnet" }.Concat(argv);
        Environment.Exit(execv(sudo, new[] { sudo }.Concat(argv).ToArray()));
    }
    if (OperatingSystem.IsWindows())
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            var argv = new[] { progname }.Concat(args);
            if (progname.EndsWith(".dll", OrdinalIgnoreCase))
                argv = new[] { "dotnet" }.Concat(argv);
            var si = new ProcessStartInfo(argv.First()) { UseShellExecute = true, Verb = "runas" };
            foreach (var arg in argv.Skip(1))
                si.ArgumentList.Add(arg);
            using var process = Process.Start(si);
            Environment.Exit(process?.HasExited == true ? process.ExitCode : 0);
        }
    }

    var nameServerSets = Preferences.NameServerSets.ToArray();
    try
    {
        if (nameServerSets.Length == 0)
            throw new InvalidOperationException("No network available.");
        Preferences.NameServerSets = nameServerSets.Select(e => (e.NetworkId, addresses.AsEnumerable()));
        Console.Error.Write("Press any key to exit:");
        await Task.WhenAny(
            Task.Run(() => Console.ReadKey(true)),
            Host.CreateDefaultBuilder()
                .ConfigureServices(services => services.AddHostedService<IdleService>())
                .RunConsoleAsync(console => console.SuppressStatusMessages = true)
            ).ConfigureAwait(false);
        Console.Error.WriteLine();
    }
    finally
    {
        Preferences.NameServerSets = nameServerSets;
    }
}
catch (Exception ex)
{
    while (ex.InnerException is not null)
        ex = ex.InnerException;
    Console.Error.WriteLine("Error: {0}", ex.Message);
}

internal sealed class IdleService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
