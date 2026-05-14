namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-3 FR-6 FR-8 TR-9 TR-10 TR-11 v1 endpoint mapping for the Distribution service runtime.</summary>
public static class SharpNinjaDistributionEndpointRouteBuilderExtensions
{
    private static readonly string[] GetMethods = [HttpMethods.Get];
    private static readonly string[] PostMethods = [HttpMethods.Post];

    /// <summary>Maps Distribution runtime endpoints for manifest retrieval, deltas, exposure ingestion, health, and metrics.</summary>
    /// <param name="endpoints">The endpoint route builder to update.</param>
    /// <returns>The updated endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapSharpNinjaFeatureFlagDistributionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapMethods("/", GetMethods, RootAsync);
        endpoints.MapMethods("/v1/manifest/{productId}/{releaseId}", GetMethods, GetManifestAsync);
        endpoints.MapMethods("/v1/manifest/{productId}/{releaseId}/delta", GetMethods, GetManifestDeltaAsync);
        endpoints.MapMethods("/v1/exposure", PostMethods, PostExposureAsync);
        endpoints.MapMethods("/health", GetMethods, HealthAsync);
        endpoints.MapMethods("/metrics", GetMethods, MetricsAsync);

        return endpoints;
    }

    private static Task RootAsync(HttpContext context) =>
        Results.Text("SharpNinja Feature Flags Distribution", "text/plain").ExecuteAsync(context);

    private static async Task GetManifestAsync(HttpContext context)
    {
        DistributionEndpointHandler handler = GetHandler(context);
        IResult result = await handler.GetManifestAsync(
            context,
            ReadRouteValue(context, "productId"),
            ReadRouteValue(context, "releaseId"),
            ReadQueryValue(context, "environment"),
            context.RequestAborted);

        await result.ExecuteAsync(context);
    }

    private static async Task GetManifestDeltaAsync(HttpContext context)
    {
        DistributionEndpointHandler handler = GetHandler(context);
        IResult result = await handler.GetManifestDeltaAsync(
            context,
            ReadRouteValue(context, "productId"),
            ReadRouteValue(context, "releaseId"),
            ReadQueryValue(context, "environment"),
            ReadQueryValue(context, "since"),
            context.RequestAborted);

        await result.ExecuteAsync(context);
    }

    private static async Task PostExposureAsync(HttpContext context)
    {
        DistributionEndpointHandler handler = GetHandler(context);
        IResult result = await handler.PostExposureAsync(context, context.RequestAborted);
        await result.ExecuteAsync(context);
    }

    private static Task HealthAsync(HttpContext context) =>
        DistributionEndpointHandler.GetHealth().ExecuteAsync(context);

    private static Task MetricsAsync(HttpContext context)
    {
        DistributionEndpointHandler handler = GetHandler(context);
        return handler.GetMetrics().ExecuteAsync(context);
    }

    private static DistributionEndpointHandler GetHandler(HttpContext context) =>
        context.RequestServices.GetRequiredService<DistributionEndpointHandler>();

    private static string ReadRouteValue(HttpContext context, string name)
    {
        object? value = context.Request.RouteValues[name];
        return value?.ToString() ?? string.Empty;
    }

    private static string? ReadQueryValue(HttpContext context, string name)
    {
        string value = context.Request.Query[name].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
