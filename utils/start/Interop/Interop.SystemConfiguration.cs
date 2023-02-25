internal static partial class Interop
{
    internal static partial class SystemConfiguration
    {
        private const string Library = "/System/Library/Frameworks/SystemConfiguration.framework/SystemConfiguration";

        private static readonly Lazy<IntPtr> Handle = new(() => NativeLibrary.Load(Library));

        internal static readonly Lazy<IntPtr> kSCPropNetDNSServerAddresses = new(() => Marshal.ReadIntPtr(NativeLibrary.GetExport(Handle.Value, nameof(kSCPropNetDNSServerAddresses))));

        internal static readonly Lazy<IntPtr> kSCNetworkProtocolTypeDNS = new(() => Marshal.ReadIntPtr(NativeLibrary.GetExport(Handle.Value, nameof(kSCNetworkProtocolTypeDNS))));

        [DllImport(Library)]
        internal static extern IntPtr SCDynamicStoreCopyValue(IntPtr store, IntPtr key);

        [DllImport(Library)]
        internal static extern IntPtr SCDynamicStoreCreate(IntPtr allocator, IntPtr name, IntPtr callout, IntPtr context);

        [DllImport(Library)]
        internal static extern bool SCDynamicStoreSetValue(IntPtr store, IntPtr key, IntPtr value);

        [DllImport(Library)]
        internal static extern IntPtr SCNetworkProtocolGetConfiguration(IntPtr protocol);

        [DllImport(Library)]
        internal static extern IntPtr SCNetworkServiceCopyAll(IntPtr prefs);

        [DllImport(Library)]
        internal static extern IntPtr SCNetworkServiceCopyProtocol(IntPtr service, IntPtr protocolType);

        [DllImport(Library)]
        internal static extern IntPtr SCNetworkServiceGetServiceID(IntPtr service);

        [DllImport(Library)]
        internal static extern IntPtr SCPreferencesCreate(IntPtr allocator, IntPtr name, IntPtr prefsID);
    }
}
