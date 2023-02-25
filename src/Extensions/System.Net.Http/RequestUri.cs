namespace System.Net.Http;

public static class RequestUri
{
    private static readonly MethodInfo EnsureUriInfo = typeof(Uri).GetMethod(nameof(EnsureUriInfo), BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo PathAndQuery = EnsureUriInfo.ReturnType.GetField(nameof(PathAndQuery), BindingFlags.Instance | BindingFlags.Public)!;

    public static Uri Create(string scheme, string host, int? port, string target)
    {
        var builder = new UriBuilder(scheme, host);
        if (port is not null)
            builder.Port = port.Value;
        var uri = builder.Uri;
        var info = EnsureUriInfo.Invoke(uri, null)!;
        PathAndQuery.SetValue(info, target);
        return uri;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Uri CopyFrom(HttpRequest request) =>
        Create(request.Scheme, request.Host.Host, request.Host.Port, request.HttpContext.Features.Get<IHttpRequestFeature>()!.RawTarget);
}
