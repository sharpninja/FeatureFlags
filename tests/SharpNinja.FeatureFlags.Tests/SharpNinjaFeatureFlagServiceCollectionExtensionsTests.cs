using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;
using SharpNinja.FeatureFlags.Evaluation;
using Xunit;

namespace SharpNinja.FeatureFlags.Tests;

/// <summary>FR-1 FR-4 FR-8 FR-10 TR-5 TR-7 TR-11 Phase 1 SDK registration tests.</summary>
public sealed class SharpNinjaFeatureFlagServiceCollectionExtensionsTests
{
    /// <summary>DI registration exposes options, manifest, evaluator, and interface client.</summary>
    [Fact]
    public void AddSharpNinjaFeatureFlagsRegistersSdkServices()
    {
        SharpNinjaFeatureFlagOptions options = CreateOptions();

        using ServiceProvider provider = new ServiceCollection()
            .AddSharpNinjaFeatureFlags(options, ManifestJson)
            .BuildServiceProvider();

        Assert.Same(options, provider.GetRequiredService<SharpNinjaFeatureFlagOptions>());
        Assert.NotNull(provider.GetRequiredService<FeatureFlagManifest>());
        Assert.NotNull(provider.GetRequiredService<FeatureFlagEvaluator>());
        Assert.IsType<SharpNinjaFeatureClient>(provider.GetRequiredService<ISharpNinjaFeatureClient>());
    }

    /// <summary>The interface client evaluates flags synchronously through the parsed manifest.</summary>
    [Fact]
    public void InterfaceClientEvaluatesFlagsSynchronously()
    {
        using ServiceProvider provider = CreateProvider();
        ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();

        EvaluationResult<bool> result = client.Evaluate("search.enabled", false);

        Assert.True(result.Value);
    }

    /// <summary>The interface client evaluates flags through the asynchronous-compatible API.</summary>
    [Fact]
    public async Task InterfaceClientEvaluatesFlagsAsynchronously()
    {
        using ServiceProvider provider = CreateProvider();
        ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();

        EvaluationResult<string> result = await client.EvaluateAsync("search.title", "Fallback");

        Assert.Equal("Search", result.Value);
    }

    /// <summary>The interface client passes the configured product id to the evaluator.</summary>
    [Fact]
    public void InterfaceClientUsesConfiguredProductId()
    {
        SharpNinjaFeatureFlagOptions options = CreateOptions() with { ProductId = "dispatch" };

        using ServiceProvider provider = new ServiceCollection()
            .AddSharpNinjaFeatureFlags(options, ManifestJson)
            .BuildServiceProvider();
        ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();

        EvaluationResult<bool> result = client.Evaluate("search.enabled", false);

        Assert.False(result.Value);
        Assert.Equal(EvaluationReason.Default, result.Reason);
    }

    /// <summary>FR-1 FR-6 verifies configured identity values override caller-supplied context values.</summary>
    [Fact]
    public void InterfaceClientIgnoresCallerIdentityOverrides()
    {
        SharpNinjaFeatureFlagOptions options = CreateOptions() with
        {
            ReleaseLineage = new("2026.05", "2.3.4", SharpNinjaReleaseChannel.Stable, "42"),
            MultiTenant = new(true, SharpNinjaEvaluationContextKeys.TenantId, "tenant-a"),
        };
        EvaluationContext callerContext = EvaluationContext.Builder()
            .Set(SharpNinjaEvaluationContextKeys.ProductId, "drivermate")
            .Set(SharpNinjaEvaluationContextKeys.ReleaseId, "wrong-release")
            .Set(SharpNinjaEvaluationContextKeys.Environment, "Production")
            .Set(SharpNinjaEvaluationContextKeys.SemanticVersion, "9.9.9")
            .Set(SharpNinjaEvaluationContextKeys.ReleaseChannel, "Beta")
            .Set(SharpNinjaEvaluationContextKeys.ReleaseBuild, "999")
            .Set(SharpNinjaEvaluationContextKeys.TenantId, "tenant-b")
            .Build();

        using ServiceProvider provider = new ServiceCollection()
            .AddSharpNinjaFeatureFlags(options, ManifestJson)
            .BuildServiceProvider();
        ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();

        Assert.True(client.Evaluate("identity.product", false, callerContext).Value);
        Assert.True(client.Evaluate("identity.release", false, callerContext).Value);
        Assert.True(client.Evaluate("identity.environment", false, callerContext).Value);
        Assert.True(client.Evaluate("identity.semantic-version", false, callerContext).Value);
        Assert.True(client.Evaluate("identity.release-channel", false, callerContext).Value);
        Assert.True(client.Evaluate("identity.release-build", false, callerContext).Value);
        Assert.True(client.Evaluate("identity.tenant", false, callerContext).Value);
    }

    /// <summary>FR-8 TR-5 TR-7 verifies synchronous evaluations are buffered as exposure events.</summary>
    [Fact]
    public void InterfaceClientBuffersExposureEvents()
    {
        DateTimeOffset now = new(2026, 5, 14, 14, 45, 0, TimeSpan.Zero);
        SharpNinjaFeatureFlagOptions options = CreateOptions() with
        {
            MultiTenant = new(true, SharpNinjaEvaluationContextKeys.TenantId, "tenant-a"),
        };
        EvaluationContext callerContext = EvaluationContext.Builder()
            .Set("project", "alpha")
            .Build();

        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FixedTimeProvider(now))
            .AddSharpNinjaFeatureFlags(options, ManifestJson)
            .BuildServiceProvider();
        ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();

        EvaluationResult<string> result = client.Evaluate("theme.mode", "fallback", callerContext);
        IReadOnlyList<SharpNinjaExposureEvent> events =
            provider.GetRequiredService<ISharpNinjaExposureEventBuffer>().Snapshot();

        SharpNinjaExposureEvent exposureEvent = Assert.Single(events);
        Assert.Equal("modern", result.Value);
        Assert.Equal("theme.mode", exposureEvent.FlagKey);
        Assert.Equal("modern", exposureEvent.ResolvedValue);
        Assert.Equal(EvaluationReason.RuleMatch, exposureEvent.Reason);
        Assert.Equal(1, exposureEvent.RuleIndex);
        Assert.Equal(64, exposureEvent.ContextFingerprint.Length);
        Assert.Equal(now, exposureEvent.Timestamp);
        Assert.Equal("truckmate", exposureEvent.ProductId);
        Assert.Equal("2026.05", exposureEvent.ReleaseId);
        Assert.Equal("Development", exposureEvent.Environment);
        Assert.Equal("tenant-a", exposureEvent.TenantId);
    }

    /// <summary>FR-8 TR-11 verifies exposure recording can be disabled through DI.</summary>
    [Fact]
    public void InterfaceClientCanDisableExposureEventsThroughDi()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<DiscardingExposureEventSink>()
            .AddSingleton<ISharpNinjaExposureEventSink>(
                static provider => provider.GetRequiredService<DiscardingExposureEventSink>())
            .AddSharpNinjaFeatureFlags(CreateOptions(), ManifestJson)
            .BuildServiceProvider();
        ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();

        EvaluationResult<bool> result = client.Evaluate("search.enabled", false);

        Assert.True(result.Value);
        Assert.IsType<DiscardingExposureEventSink>(
            provider.GetRequiredService<ISharpNinjaExposureEventSink>());
        Assert.Empty(provider.GetRequiredService<ISharpNinjaExposureEventBuffer>().Snapshot());
    }

    private static ServiceProvider CreateProvider() =>
        new ServiceCollection()
            .AddSharpNinjaFeatureFlags(CreateOptions(), ManifestJson)
            .BuildServiceProvider();

    private static SharpNinjaFeatureFlagOptions CreateOptions() =>
        new(
            ProductId: "truckmate",
            ReleaseId: "2026.05",
            Environment: "Development",
            ManifestRefreshInterval: TimeSpan.FromMinutes(5),
            ExposureUploadInterval: TimeSpan.FromMinutes(1));

    private const string ManifestJson =
        """
        {
          "schemaVersion": 1,
          "productId": "truckmate",
          "releaseId": "2026.05",
          "environment": "Development",
          "flags": [
            {
              "key": "search.enabled",
              "type": "boolean",
              "defaultValue": true,
              "killable": true,
              "productScope": [ "truckmate" ]
            },
            {
              "key": "search.title",
              "type": "string",
              "defaultValue": "Search",
              "killable": false,
              "productScope": [ "truckmate" ]
            },
            {
              "key": "identity.product",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "ProductId == 'truckmate'",
                  "value": true
                }
              ]
            },
            {
              "key": "identity.release",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "ReleaseId == '2026.05'",
                  "value": true
                }
              ]
            },
            {
              "key": "identity.environment",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "Environment == 'Development'",
                  "value": true
                }
              ]
            },
            {
              "key": "identity.semantic-version",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "SemanticVersion == '2.3.4'",
                  "value": true
                }
              ]
            },
            {
              "key": "identity.release-channel",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "ReleaseChannel == 'Stable'",
                  "value": true
                }
              ]
            },
            {
              "key": "identity.release-build",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "ReleaseBuild == '42'",
                  "value": true
                }
              ]
            },
            {
              "key": "identity.tenant",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "TenantId == 'tenant-a'",
                  "value": true
                }
              ]
            },
            {
              "key": "theme.mode",
              "type": "string",
              "defaultValue": "classic",
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "project == 'beta'",
                  "value": "beta"
                },
                {
                  "when": "project == 'alpha'",
                  "value": "modern"
                }
              ]
            }
          ]
        }
        """;

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class DiscardingExposureEventSink : ISharpNinjaExposureEventSink
    {
        public void Record(SharpNinjaExposureEvent exposureEvent)
        {
            ArgumentNullException.ThrowIfNull(exposureEvent);
        }
    }
}
