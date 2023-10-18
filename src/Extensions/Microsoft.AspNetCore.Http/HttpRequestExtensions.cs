namespace Microsoft.AspNetCore.Http;

public static partial class HttpRequestExtensions
{
    public static string GetRawUri(this HttpRequest request)
    {
        var target = request.HttpContext.Features.Get<IHttpRequestFeature>()!.RawTarget;
        if (!target.StartsWith('/'))
            return target;
        var (scheme, host) = (request.Scheme, request.Host.Value);
        if (request.HttpContext.WebSockets.IsWebSocketRequest)
            scheme = scheme.Replace(Uri.UriSchemeHttp, Uri.UriSchemeWs);
        return new StringBuilder(scheme.Length + Uri.SchemeDelimiter.Length + host.Length + target.Length)
            .Append(scheme)
            .Append(Uri.SchemeDelimiter)
            .Append(host)
            .Append(target)
            .ToString();
    }
}
