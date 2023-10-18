namespace System.Net.Http.Headers;

public static partial class MediaTypeHeaderValueExtensions
{
    private static readonly Encoding DefaultEncoding = Encoding.UTF8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetEncoding(this MediaTypeHeaderValue contentType, [MaybeNullWhen(false)] out Encoding encoding)
    {
        try
        {
            encoding = contentType.CharSet is string charset ? Encoding.GetEncoding(charset) : DefaultEncoding;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            encoding = default;
            return false;
        }
    }
}
