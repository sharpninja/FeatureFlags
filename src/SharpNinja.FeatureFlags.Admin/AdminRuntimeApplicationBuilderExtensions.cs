using System.Globalization;
using System.Text;

namespace SharpNinja.FeatureFlags.Admin;

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-10 TR-11: Lightweight Admin runtime request handler registration.</summary>
/// <remarks>
/// Registration is idempotent for the admin-runtime middleware it owns; call once per pipeline.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-11"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public static class AdminRuntimeApplicationBuilderExtensions
{
    /// <summary>Registers lightweight Admin runtime request handlers for operator smoke tests and metrics scraping.</summary>
    /// <param name="app">Application builder to update.</param>
    /// <returns>The updated application builder.</returns>
    public static IApplicationBuilder UseSharpNinjaFeatureFlagsAdminRuntime(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        IAdminRuntimeService runtime = app.ApplicationServices.GetRequiredService<IAdminRuntimeService>();
        app.Run(context => HandleRequestAsync(context, runtime));

        return app;
    }

    private static Task HandleRequestAsync(HttpContext context, IAdminRuntimeService runtime)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        PathString path = context.Request.Path;
        if (path == "/")
        {
            return WriteTextAsync(context, "SharpNinja Feature Flags Admin");
        }

        bool isAdminPath = path == "/admin/runtime"
            || path == "/admin/audit"
            || path == "/admin/metrics";
        if (isAdminPath && (context.User.Identity is null || !context.User.Identity.IsAuthenticated))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        if (path == "/admin/runtime")
        {
            return WriteTextAsync(context, CreateRuntimeResponse(runtime));
        }

        if (path == "/admin/audit")
        {
            return WriteTextAsync(context, CreateAuditResponse(runtime));
        }

        if (path == "/admin/metrics")
        {
            context.Response.ContentType = "text/plain; version=0.0.4";
            return context.Response.WriteAsync(CreateMetricsResponse(runtime), context.RequestAborted);
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    private static Task WriteTextAsync(HttpContext context, string text)
    {
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(text, context.RequestAborted);
    }

    private static string CreateRuntimeResponse(IAdminRuntimeService runtime)
    {
        AdminRuntimeMetrics metrics = runtime.GetMetrics();
        return string.Format(
            CultureInfo.InvariantCulture,
            "SharpNinja Feature Flags Admin Runtime{0}drafts {1}{0}audit_entries {2}{0}publishes {3}{0}promotions {4}{0}",
            Environment.NewLine,
            metrics.DraftCount,
            metrics.AuditEntryCount,
            metrics.PublishCount,
            metrics.PromotionCount);
    }

    private static string CreateAuditResponse(IAdminRuntimeService runtime)
    {
        IReadOnlyList<AdminAuditEntry> entries = runtime.GetAuditTrail();
        if (entries.Count == 0)
        {
            return "No admin audit entries recorded.";
        }

        var builder = new StringBuilder();
        foreach (AdminAuditEntry entry in entries)
        {
            builder.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0} {1} {2} {3}",
                entry.Sequence,
                entry.Action,
                entry.FlagKey,
                entry.EnvironmentName);

            if (!string.IsNullOrWhiteSpace(entry.TargetEnvironmentName))
            {
                builder.Append(" -> ");
                builder.Append(entry.TargetEnvironmentName);
            }

            builder.Append(' ');
            builder.Append(entry.RbacMetadata.TenantId);
            builder.Append('/');
            builder.Append(entry.RbacMetadata.PrincipalId);
            builder.Append(' ');
            builder.AppendLine(entry.Reason);
        }

        return builder.ToString();
    }

    private static string CreateMetricsResponse(IAdminRuntimeService runtime)
    {
        AdminRuntimeMetrics metrics = runtime.GetMetrics();
        var builder = new StringBuilder();
        AppendMetric(builder, "sharpninja_admin_drafts", "Current in-memory Admin flag drafts.", metrics.DraftCount);
        AppendMetric(builder, "sharpninja_admin_audit_entries", "Current append-only Admin audit entries.", metrics.AuditEntryCount);
        AppendMetric(builder, "sharpninja_admin_publishes", "Admin publish audit entries.", metrics.PublishCount);
        AppendMetric(builder, "sharpninja_admin_promotions", "Admin promotion audit entries.", metrics.PromotionCount);

        return builder.ToString();
    }

    private static void AppendMetric(StringBuilder builder, string name, string help, int value)
    {
        builder.Append("# HELP ");
        builder.Append(name);
        builder.Append(' ');
        builder.AppendLine(help);
        builder.Append("# TYPE ");
        builder.Append(name);
        builder.AppendLine(" gauge");
        builder.Append(name);
        builder.Append(' ');
        builder.AppendLine(value.ToString(CultureInfo.InvariantCulture));
    }
}
