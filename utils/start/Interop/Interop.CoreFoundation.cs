internal static partial class Interop
{
    [SuppressMessage("Globalization", "CA2101")]
    internal static partial class CoreFoundation
    {
        private const string Library = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        internal const uint kCFStringEncodingUTF8 = 0x08000100;

        private static readonly Lazy<IntPtr> Handle = new(() => NativeLibrary.Load(Library));

        internal static readonly Lazy<IntPtr> kCFTypeArrayCallBacks = new(() => NativeLibrary.GetExport(Handle.Value, nameof(kCFTypeArrayCallBacks)));

        internal static readonly Lazy<IntPtr> kCFTypeDictionaryKeyCallBacks = new(() => NativeLibrary.GetExport(Handle.Value, nameof(kCFTypeDictionaryKeyCallBacks)));
        internal static readonly Lazy<IntPtr> kCFTypeDictionaryValueCallBacks = new(() => NativeLibrary.GetExport(Handle.Value, nameof(kCFTypeDictionaryValueCallBacks)));

        [DllImport(Library)]
        internal static extern IntPtr CFArrayCreate(IntPtr allocator, IntPtr[] values, nint numValues, IntPtr callBacks);

        [DllImport(Library)]
        internal static extern nint CFArrayGetCount(IntPtr theArray);

        [DllImport(Library)]
        internal static extern nuint CFArrayGetTypeID();

        [DllImport(Library)]
        internal static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, nint idx);

        [DllImport(Library)]
        internal static extern void CFDictionaryAddValue(IntPtr theDict, IntPtr key, IntPtr value);

        [DllImport(Library)]
        internal static extern IntPtr CFDictionaryCreateMutable(IntPtr allocator, nint capacity, IntPtr keyCallBacks, IntPtr valueCallBacks);

        [DllImport(Library)]
        internal static extern IntPtr CFDictionaryCreateMutableCopy(IntPtr allocator, nint capacity, IntPtr theDict);

        [DllImport(Library)]
        internal static extern bool CFDictionaryGetValueIfPresent(IntPtr theDict, IntPtr key, ref IntPtr value);

        [DllImport(Library)]
        internal static extern void CFDictionaryRemoveValue(IntPtr theDict, IntPtr key);

        [DllImport(Library)]
        internal static extern nuint CFGetTypeID(IntPtr cf);

        [DllImport(Library)]
        internal static extern void CFRelease(IntPtr cf);

        [DllImport(Library)]
        internal static extern IntPtr CFRetain(IntPtr cf);

        [DllImport(Library)]
        internal static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, uint encoding);

        [DllImport(Library)]
        internal static extern bool CFStringGetCString(IntPtr theString, IntPtr buffer, nint bufferSize, uint encoding);

        [DllImport(Library)]
        internal static extern IntPtr CFStringGetCStringPtr(IntPtr theString, uint encoding);

        [DllImport(Library)]
        internal static extern nint CFStringGetLength(IntPtr theString);

        [DllImport(Library)]
        internal static extern nint CFStringGetMaximumSizeForEncoding(nint length, uint encoding);

        [DllImport(Library)]
        internal static extern nuint CFStringGetTypeID();

        internal static void CFSafeRelease(IntPtr cf)
        {
            if (cf != default)
                CFRelease(cf);
        }

        internal static string? CFStringGetString(IntPtr cf)
        {
            if (cf == default)
                return null;
            var ptr = CFStringGetCStringPtr(cf, kCFStringEncodingUTF8);
            if (ptr != default)
                return Marshal.PtrToStringUTF8(ptr);
            var size = (int)CFStringGetMaximumSizeForEncoding(CFStringGetLength(cf), kCFStringEncodingUTF8);
            var buffer = Marshal.AllocCoTaskMem(size);
            var ok = CFStringGetCString(cf, buffer, size, kCFStringEncodingUTF8);
            var str = ok ? Marshal.PtrToStringUTF8(buffer) : null;
            Marshal.FreeCoTaskMem(buffer);
            return str;
        }
    }
}
