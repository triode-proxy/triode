internal record Record(string Id, long Time, IPAddress? From, string Method, string Uri, string Protocol, int Status, long Size, long Elapsed,
    IEnumerable<KeyValuePair<string, StringValues>> RequestHeaders, IEnumerable<KeyValuePair<string, StringValues>> ResponseHeaders)
{
    internal ReadOnlyMemory<byte> Memory => JsonSerializer.SerializeToUtf8Bytes(new object[]
    {
        Id, Time, $"{From}", Method, Uri, Protocol, Status, Size, Elapsed, Flatten(RequestHeaders), Flatten(ResponseHeaders),
    });

    private static IEnumerable<IEnumerable<string>> Flatten(IEnumerable<KeyValuePair<string, StringValues>> headers)
    {
        foreach (var (name, values) in headers)
            foreach (var value in values)
                yield return new[] { name, value };
    }
}
