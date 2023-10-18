namespace System.Text.RegularExpressions;

public static partial class RegexExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetValue<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> collection, string key, [MaybeNullWhen(false)] out TValue value) where TKey : Regex
    {
        (TKey pattern, value) = collection.FirstOrDefault(p => p.Key.IsMatch(key));
        return pattern is not null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryMatch(this Regex regex, string input, out Match match) => (match = regex.Match(input)).Success;
}
