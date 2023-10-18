namespace System.Linq;

public static partial class EnumerableExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Except<TSource>(this IEnumerable<TSource> source, params TSource[] values) =>
        source.Except(values.AsEnumerable());
}
