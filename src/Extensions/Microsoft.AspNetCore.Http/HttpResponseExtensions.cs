namespace Microsoft.AspNetCore.Http;

public static partial class HttpResponseExtensions
{
    public static void End(this HttpResponse response, int statusCode)
    {
        if (!response.HasStarted)
        {
            var server = response.Headers.Server;
            response.Clear();
            response.StatusCode = statusCode;
            response.ContentLength = 0;
            response.Headers.Server = server;
        }
    }
}
