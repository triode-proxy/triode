namespace System.Net.Http.Headers;

public static partial class HttpContentHeadersExtensions
{
    public static bool TryAddIfNotPresent(this HttpContentHeaders headers, string name, IEnumerable<string?> values)
    {
        // https://github.com/dotnet/runtime/issues/16162
        var present = headers.Any(p => p.Key.Equals(name, OrdinalIgnoreCase))
            || name.Equals(HeaderNames.ContentLength, OrdinalIgnoreCase) && headers.ContentLength is not null;
        return !present && headers.TryAddWithoutValidation(name, values);
    }
}
