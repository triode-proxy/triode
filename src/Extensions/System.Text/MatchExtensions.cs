namespace System.Text;

public static partial class MatchExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetValue<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> collection, string key, [MaybeNullWhen(false)] out TValue value) where TKey : IMatch
    {
        (var pattern, value) = collection.FirstOrDefault(p => p.Key.IsMatch(key));
        return pattern is not null;
    }
}
