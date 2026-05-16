using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharpNinja.FeatureFlags.Distribution;
using Xunit;

namespace SharpNinja.FeatureFlags.Distribution.Tests;

/// <summary>FR-8 TR-10 tests for ngrok forwarded-headers middleware registration on the Distribution service.</summary>
public sealed class NgrokTunnelingExtensionsTests
{
    /// <summary>FR-8 TR-10 AddNgrokTunneling enables X-Forwarded-For header forwarding.</summary>
    [Fact]
    public void AddNgrokTunnelingConfiguresXForwardedFor()
    {
        IOptions<ForwardedHeadersOptions> options = BuildOptions();

        Assert.True(options.Value.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
    }

    /// <summary>FR-8 TR-10 AddNgrokTunneling enables X-Forwarded-Proto header forwarding.</summary>
    [Fact]
    public void AddNgrokTunnelingConfiguresXForwardedProto()
    {
        IOptions<ForwardedHeadersOptions> options = BuildOptions();

        Assert.True(options.Value.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
    }

    /// <summary>FR-8 TR-10 AddNgrokTunneling clears KnownIPNetworks so any upstream proxy is trusted.</summary>
    [Fact]
    public void AddNgrokTunnelingClearsKnownIPNetworks()
    {
        IOptions<ForwardedHeadersOptions> options = BuildOptions();

        Assert.Empty(options.Value.KnownIPNetworks);
    }

    /// <summary>FR-8 TR-10 AddNgrokTunneling clears KnownProxies so any upstream proxy is trusted.</summary>
    [Fact]
    public void AddNgrokTunnelingClearsKnownProxies()
    {
        IOptions<ForwardedHeadersOptions> options = BuildOptions();

        Assert.Empty(options.Value.KnownProxies);
    }

    /// <summary>FR-8 TR-10 AddNgrokTunneling returns the same IServiceCollection for fluent chaining.</summary>
    [Fact]
    public void AddNgrokTunnelingReturnsServiceCollection()
    {
        var services = new ServiceCollection();

        IServiceCollection result = services.AddNgrokTunneling();

        Assert.Same(services, result);
    }

    private static IOptions<ForwardedHeadersOptions> BuildOptions()
    {
        var services = new ServiceCollection();
        services.AddNgrokTunneling();
        return services.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>();
    }
}
