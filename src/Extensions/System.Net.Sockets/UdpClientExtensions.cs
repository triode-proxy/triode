namespace System.Net.Sockets;

public static partial class UdpClientExtensions
{
    private const int IOC_IN            = unchecked((int)0x80000000);
    private const int IOC_VENDOR        = unchecked((int)0x18000000);
    private const int SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;

    public static void DisableConnectionResetReporting(this UdpClient client)
    {
        if (OperatingSystem.IsWindows())
            client.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, new byte[] { 0 });
    }
}
