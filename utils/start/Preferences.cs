internal static class Preferences
{
    private static readonly string AssemblyName = typeof(Preferences).Assembly.GetName()!.Name!;

    internal static IEnumerable<(string NetworkId, IEnumerable<IPAddress> Addresses)> NameServerSets
    {
        get
        {
            if (OperatingSystem.IsMacOS())
            {
                var name = CFStringCreateWithCString(default, AssemblyName, kCFStringEncodingUTF8);
                var prefs = SCPreferencesCreate(default, name, default);
                var store = SCDynamicStoreCreate(default, name, default, default);
                var services = prefs != default ? SCNetworkServiceCopyAll(prefs) : default;
                CFRelease(name);
                try
                {
                    if (services == default || store == default)
                        throw new InvalidOperationException();
                    for (nint serviceIndex = 0, serviceCount = CFArrayGetCount(services); serviceIndex < serviceCount; serviceIndex++)
                    {
                        var service = CFArrayGetValueAtIndex(services, serviceIndex);
                        var id = CFStringGetString(SCNetworkServiceGetServiceID(service));
                        if (id is null)
                            continue;
                        var isDnsEnabled = false;
                        var dnsAddresses = new List<IPAddress>();
                        var key = CFStringCreateWithCString(default, $"State:/Network/Service/{id}/DNS", kCFStringEncodingUTF8);
                        var state = SCDynamicStoreCopyValue(store, key);
                        CFRelease(key);
                        if (state != default)
                        {
                            var addrs = default(IntPtr);
                            if (CFDictionaryGetValueIfPresent(state, kSCPropNetDNSServerAddresses.Value, ref addrs))
                            {
                                Debug.Assert(CFGetTypeID(addrs) == CFArrayGetTypeID());
                                isDnsEnabled = true;
                            }
                            CFRelease(state);
                        }
                        var proto = SCNetworkServiceCopyProtocol(service, kSCNetworkProtocolTypeDNS.Value);
                        if (proto != default)
                        {
                            var conf = SCNetworkProtocolGetConfiguration(proto);
                            if (conf != default)
                            {
                                var addrs = default(IntPtr);
                                if (CFDictionaryGetValueIfPresent(conf, kSCPropNetDNSServerAddresses.Value, ref addrs))
                                {
                                    Debug.Assert(CFGetTypeID(addrs) == CFArrayGetTypeID());
                                    isDnsEnabled = true;
                                    for (nint i = 0, n = CFArrayGetCount(addrs); i < n; i++)
                                    {
                                        var addr = CFArrayGetValueAtIndex(addrs, i);
                                        Debug.Assert(CFGetTypeID(addr) == CFStringGetTypeID());
                                        dnsAddresses.Add(IPAddress.Parse(CFStringGetString(addr)!));
                                    }
                                }
                            }
                            CFRelease(proto);
                        }
                        if (isDnsEnabled)
                            yield return (id, dnsAddresses);
                    }
                }
                finally
                {
                    CFSafeRelease(services);
                    CFSafeRelease(store);
                    CFSafeRelease(prefs);
                }
                yield break;
            }
            if (OperatingSystem.IsWindows())
            {
                foreach (var netif in NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.OperationalStatus == OperationalStatus.Up))
                {
                    var props = netif.GetIPProperties();
                    if (!props.GatewayAddresses.Any())
                        continue;
                    if (props.IsDynamicDnsEnabled)
                        yield return (netif.Id, Array.Empty<IPAddress>());
                    else if (props.DnsAddresses.Count > 0)
                        yield return (netif.Id, props.DnsAddresses);
                }
                yield break;
            }
            throw new PlatformNotSupportedException();
        }
        set
        {
            if (OperatingSystem.IsMacOS())
            {
                var name = CFStringCreateWithCString(default, AssemblyName, kCFStringEncodingUTF8);
                var store = SCDynamicStoreCreate(default, name, default, default);
                CFRelease(name);
                if (store == default)
                    throw new InvalidOperationException();
                var ok = true;
                foreach (var (id, addresses) in value)
                {
                    var key = CFStringCreateWithCString(default, $"Setup:/Network/Service/{id}/DNS", kCFStringEncodingUTF8);
                    var oldValue = SCDynamicStoreCopyValue(store, key);
                    var newValue = oldValue == default
                        ? CFDictionaryCreateMutable(default, 0, kCFTypeDictionaryKeyCallBacks.Value, kCFTypeDictionaryValueCallBacks.Value)
                        : CFDictionaryCreateMutableCopy(default, 0, oldValue);
                    CFSafeRelease(oldValue);
                    CFDictionaryRemoveValue(newValue, kSCPropNetDNSServerAddresses.Value);
                    if (addresses.Any())
                    {
                        var items = addresses.Select(a => CFStringCreateWithCString(default, a.ToString(), kCFStringEncodingUTF8)).ToArray();
                        var array = CFArrayCreate(default, items, items.Length, kCFTypeArrayCallBacks.Value);
                        foreach (var item in items)
                            CFRelease(item);
                        CFDictionaryAddValue(newValue, kSCPropNetDNSServerAddresses.Value, array);
                    }
                    ok = ok && SCDynamicStoreSetValue(store, key, newValue);
                    CFRelease(newValue);
                    CFRelease(key);
                }
                CFRelease(store);
                if (!ok)
                    throw new UnauthorizedAccessException();
                return;
            }
            if (OperatingSystem.IsWindows())
            {
                foreach (var (id, addresses) in value)
                {
                    using var tcpip4 = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{id}", writable: true);
                    using var tcpip6 = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces\{id}", writable: true);
                    tcpip4?.SetValue("NameServer", string.Join(',', addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork)));
                    tcpip6?.SetValue("NameServer", string.Join(',', addresses.Where(a => a.AddressFamily == AddressFamily.InterNetworkV6)));
                }
                return;
            }
            throw new PlatformNotSupportedException();
        }
    }
}
