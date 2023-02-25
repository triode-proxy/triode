namespace System.Text.RegularExpressions;

public class Wildcard : Regex
{
    public string OriginalString { get; }

    public Wildcard(string pattern, RegexOptions options = default)
        : base("^(?:" + Escape(pattern).Replace("\\?", ".").Replace("\\*", ".*").Replace(',', '|') + ")$", options)
    {
        OriginalString = pattern;
    }

    public override string ToString() => OriginalString;
}
