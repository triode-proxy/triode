internal static partial class Interop
{
    [SuppressMessage("Globalization", "CA2101")]
    internal static partial class Sys
    {
        private const string Library = "libc";

        [DllImport(Library)]
        internal static extern int execv(string path, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string?[] argv);

        [DllImport(Library)]
        internal static extern uint geteuid();
    }
}
