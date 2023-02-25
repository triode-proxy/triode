namespace System.Linq;

public static class EnumerableExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Except<TSource>(this IEnumerable<TSource> source, params TSource[] values) =>
        source.Except(values.AsEnumerable());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<KeyValuePair<TKey, TValue>> Except<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source, IEnumerable<TKey> keys) =>
        source.Where(p => !keys.Contains(p.Key));
}
