namespace System.Net;

public sealed class Domain : IMatch
{
    public string Pattern { get; }

    public Domain(string pattern) => Pattern = pattern;

    public bool IsMatch(string input) => Pattern.StartsWith("*.", Ordinal)
        ? Pattern.AsSpan(2).Equals(input, OrdinalIgnoreCase) ||
          Pattern.Length <= input.Length &&
          Pattern.AsSpan(1).Equals(input.AsSpan(input.Length + 1 - Pattern.Length), OrdinalIgnoreCase)
        : Pattern.Equals(input, OrdinalIgnoreCase);

    public override string ToString() => Pattern;
}
