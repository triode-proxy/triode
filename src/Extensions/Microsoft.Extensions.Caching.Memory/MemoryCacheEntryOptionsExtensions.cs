namespace Microsoft.Extensions.Caching.Memory;

public static class MemoryCacheEntryOptionsExtensions
{
    private static readonly PostEvictionDelegate Dispose = (_, value, _, _) => (value as IDisposable)?.Dispose();

    public static MemoryCacheEntryOptions DisposeOnEvicted(this MemoryCacheEntryOptions options) =>
        options.RegisterPostEvictionCallback(Dispose);
}
