namespace Microsoft.AspNetCore.Builder;

public static partial class StaticFileOptionsExtensions
{
    public static StaticFileOptions WithCacheControl(this StaticFileOptions options, string value)
    {
        options.OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = value;
        return options;
    }
}
