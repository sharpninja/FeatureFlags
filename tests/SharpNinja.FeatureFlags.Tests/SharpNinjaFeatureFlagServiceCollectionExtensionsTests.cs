using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;
using SharpNinja.FeatureFlags.Evaluation;
using Xunit;

namespace SharpNinja.FeatureFlags.Tests;

/// <summary>FR-4 TR-11 Phase 1 SDK registration tests.</summary>
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
            }
          ]
        }
        """;
}
