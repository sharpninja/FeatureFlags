using System.Globalization;
using System.Text;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed class ManifestJsonResult : IResult
{
    private readonly DistributionManifest manifest;
    private readonly SharpNinjaDistributionOptions options;

    public ManifestJsonResult(DistributionManifest manifest, SharpNinjaDistributionOptions options)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(options);
        this.manifest = manifest;
        this.options = options;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ApplyHeaders(httpContext.Response, manifest, options);
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        return httpContext.Response.WriteAsync(manifest.Json, Encoding.UTF8, httpContext.RequestAborted);
    }

    public static void ApplyHeaders(
        HttpResponse response,
        DistributionManifest manifest,
        SharpNinjaDistributionOptions options)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(options);

        response.Headers["ETag"] = manifest.ETag;
        response.Headers["Last-Modified"] = manifest.UpdatedAt.UtcDateTime.ToString("R", CultureInfo.InvariantCulture);
        response.Headers["Cache-Control"] = CreateCacheControl(options);
        response.Headers["Vary"] = string.Join(
            ", ",
            "Authorization",
            SharpNinjaDistributionHeaders.ProductApiKeyHeaderName,
            "X-Api-Key",
            SharpNinjaDistributionHeaders.DevicePlatformHeaderName);
        response.Headers["X-Content-Type-Options"] = "nosniff";
    }

    private static string CreateCacheControl(SharpNinjaDistributionOptions options)
    {
        if (!options.EnableCdnCacheHeaders)
        {
            return "no-cache";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"public, max-age={(int)options.ManifestMaxAge.TotalSeconds}, stale-while-revalidate={(int)options.ManifestStaleWhileRevalidate.TotalSeconds}, stale-if-error={(int)options.ManifestStaleIfError.TotalSeconds}");
    }
}
