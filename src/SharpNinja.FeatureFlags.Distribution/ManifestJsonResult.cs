using System.Globalization;
using System.Text;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed class ManifestJsonResult : IResult
{
    private readonly DistributionManifest manifest;

    public ManifestJsonResult(DistributionManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        this.manifest = manifest;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ApplyHeaders(httpContext.Response, manifest);
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        return httpContext.Response.WriteAsync(manifest.Json, Encoding.UTF8, httpContext.RequestAborted);
    }

    public static void ApplyHeaders(HttpResponse response, DistributionManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(manifest);

        response.Headers["ETag"] = manifest.ETag;
        response.Headers["Last-Modified"] = manifest.UpdatedAt.UtcDateTime.ToString("R", CultureInfo.InvariantCulture);
        response.Headers["Cache-Control"] = "no-cache";
    }
}
