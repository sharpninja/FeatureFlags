using Microsoft.AspNetCore.HttpOverrides;

namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-8 TR-10 registers and applies forwarded-headers middleware for ngrok and reverse-proxy hosting.</summary>
public static class NgrokTunnelingExtensions
{
    /// <summary>
    /// Registers <see cref="ForwardedHeadersOptions"/> configured for ngrok: accepts
    /// <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> from any upstream proxy.
    /// Call before AddSharpNinjaFeatureFlagDistribution.
    /// </summary>
    public static IServiceCollection AddNgrokTunneling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    /// <summary>
    /// Applies <see cref="ForwardedHeadersMiddleware"/> to the pipeline.
    /// Must be called before MapSharpNinjaFeatureFlagDistributionEndpoints.
    /// </summary>
    public static IApplicationBuilder UseNgrokTunneling(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseForwardedHeaders();
    }
}
