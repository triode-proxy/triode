internal sealed class Criteria
{
    public string? Method { get; set; }
    public Wildcard? Host { get; set; }
    public Wildcard? Path { get; set; }

    public bool IsMatch(HttpRequest request)
    {
        if (Method?.Equals(request.Method, Ordinal) == false)
            return false;
        if (Host?.IsMatch(request.Host.Host) == false)
            return false;
        if (Path?.IsMatch(request.Path) == false)
            return false;
        return true;
    }
}
