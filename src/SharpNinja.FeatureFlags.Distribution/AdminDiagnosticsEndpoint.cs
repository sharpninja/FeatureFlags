using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>TR-10 TR-11: Protected /admin/diagnostics endpoint surfacing per-tenant Distribution counters.</summary>
/// <remarks>
/// Stateless after construction; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public static class AdminDiagnosticsEndpoint
{
    private static readonly string[] GetMethods = [HttpMethods.Get];

    /// <summary>Maps the protected diagnostics endpoint. Caller must register authentication + authorization before mapping.</summary>
    /// <param name="endpoints">Endpoint route builder.</param>
    /// <returns>The updated endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapSharpNinjaAdminDiagnostics(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapMethods("/admin/diagnostics", GetMethods, HandleAsync)
            .RequireAuthorization(new AuthorizeAttribute());

        return endpoints;
    }

    private static Task HandleAsync(HttpContext context)
    {
        DistributionMetrics metrics = context.RequestServices.GetRequiredService<DistributionMetrics>();
        IDistributionManifestRegistry manifestRegistry = context.RequestServices.GetRequiredService<IDistributionManifestRegistry>();
        IExposureEventStore exposureStore = context.RequestServices.GetRequiredService<IExposureEventStore>();
        IOptions<SharpNinjaDistributionOptions> options = context.RequestServices.GetRequiredService<IOptions<SharpNinjaDistributionOptions>>();

        string tenant = context.User.FindFirstValue(SharpNinjaDistributionAdminClaims.TenantClaimType) ?? "unknown";
        string principal = context.User.Identity?.Name
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "unknown";

        var builder = new StringBuilder();
        builder.AppendLine("# HELP sharpninja_distribution_admin_request_tenant Tenant identifier of the caller of /admin/diagnostics.");
        builder.AppendLine("# TYPE sharpninja_distribution_admin_request_tenant gauge");
        builder.Append("sharpninja_distribution_admin_request_tenant{tenant=\"")
            .Append(tenant)
            .Append("\",principal=\"")
            .Append(principal)
            .Append("\"} 1")
            .AppendLine();
        builder.AppendLine("# HELP sharpninja_distribution_admin_manifest_count Total manifests visible to /admin/diagnostics.");
        builder.AppendLine("# TYPE sharpninja_distribution_admin_manifest_count gauge");
        builder.Append("sharpninja_distribution_admin_manifest_count ")
            .Append(manifestRegistry.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine();
        builder.AppendLine("# HELP sharpninja_distribution_admin_exposure_count Total exposure events visible to /admin/diagnostics.");
        builder.AppendLine("# TYPE sharpninja_distribution_admin_exposure_count gauge");
        builder.Append("sharpninja_distribution_admin_exposure_count ")
            .Append(exposureStore.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine();
        builder.Append(metrics.RenderPrometheus(manifestRegistry, exposureStore, options));

        context.Response.ContentType = "text/plain; version=0.0.4";
        return context.Response.WriteAsync(builder.ToString(), context.RequestAborted);
    }
}

/// <summary>TR-10 TR-11: claim type constants the Distribution service expects from upstream Admin IdentityServer tokens.</summary>
/// <remarks>
/// Stateless after construction; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public static class SharpNinjaDistributionAdminClaims
{
    /// <summary>Tenant identifier claim shared with the Admin runtime.</summary>
    public const string TenantClaimType = "sharpninja:tenant";

    /// <summary>Products grant claim shared with the Admin runtime.</summary>
    public const string ProductsClaimType = "sharpninja:products";
}
