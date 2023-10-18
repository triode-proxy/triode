namespace System.IO;

public static partial class StreamReaderExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<string> ReadLines(this StreamReader reader)
    {
        for (var line = default(string?); (line = reader.ReadLine()) is not null; )
            yield return line;
    }
}
