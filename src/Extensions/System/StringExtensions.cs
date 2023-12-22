namespace System;

public static partial class StringExtensions
{
    private static readonly IReadOnlyCollection<string> TwoLetterWords = new[]
    {
        "ad", "an", "as", "at",
        "be", "by",
        "do",
        "go",
        "he", "hi",
        "id", "if", "in", "is", "it",
        "me", "mr", "ms", "my",
        "no",
        "of", "oh", "on", "ox",
        "so",
        "to",
        "up", "us",
        "we",
    };

    public static string Capitalize(this string input, CultureInfo culture) => string.Create(input.Length, input, (chars, s) =>
    {
        var start = s.TakeWhile(c => !char.IsLetterOrDigit(c)).Count();
        s.AsSpan(0, start).CopyTo(chars);
        while (start < chars.Length)
        {
            var end = start + s.Skip(start).TakeWhile(c => char.IsLetterOrDigit(c)).Count();
            if (start + 2 == end && !TwoLetterWords.Any(w => s.AsSpan(start, 2).Equals(w, OrdinalIgnoreCase)))
            {
                chars[start] = char.ToUpper(s[start++], culture);
                chars[start] = char.ToUpper(s[start++], culture);
            }
            else
            {
                chars[start] = char.ToUpper(s[start++], culture);
                s.AsSpan()[start..end].ToLower(chars[start..end], culture);
            }
            start = end + s.Skip(end).TakeWhile(c => !char.IsLetterOrDigit(c)).Count();
            s.AsSpan()[end..start].CopyTo(chars[end..start]);
        }
    });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Capitalize(this string input) => input.Capitalize(CultureInfo.CurrentCulture);
}
