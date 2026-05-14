using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Distribution;
using Xunit;

namespace SharpNinja.FeatureFlags.Distribution.Tests;

/// <summary>FR-3 FR-6 FR-8 TR-9 TR-10 TR-11 Distribution runtime endpoint tests.</summary>
public sealed class DistributionEndpointHandlerTests
{
    private const string ProductId = "truckmate";
    private const string ReleaseId = "truckmate-1.2.3-stable-4";
    private const string EnvironmentName = "Development";
    private const string ApiKey = "test-product-key";

    private const string ManifestJson =
        """
        {
          "schemaVersion": 1,
          "productId": "truckmate",
          "releaseId": "truckmate-1.2.3-stable-4",
          "environment": "Development",
          "flags": []
        }
        """;

    /// <summary>FR-3 FR-6 TR-9 resolves a registered manifest with default environment and ETag headers.</summary>
    [Fact]
    public async Task ManifestLookupReturnsRegisteredManifest()
    {
        using DistributionTestScope scope = CreateScope();
        DistributionManifest manifest = await scope.RegisterManifestAsync();
        DefaultHttpContext context = scope.CreateContext();

        IResult result = await scope.Handler.GetManifestAsync(
            context,
            ProductId,
            ReleaseId,
            environment: null,
            CancellationToken.None);

        (int statusCode, string body) = await ExecuteAsync(result, context);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal(manifest.ETag, context.Response.Headers["ETag"]);
        Assert.Contains("\"productId\": \"truckmate\"", body, StringComparison.Ordinal);
        Assert.Contains("\"releaseId\": \"truckmate-1.2.3-stable-4\"", body, StringComparison.Ordinal);
    }

    /// <summary>FR-6 TR-9 rejects product/release mismatches by missing the in-memory manifest tuple.</summary>
    [Fact]
    public async Task ProductReleaseMismatchReturnsNotFound()
    {
        using DistributionTestScope scope = CreateScope();
        await scope.RegisterManifestAsync();
        DefaultHttpContext context = scope.CreateContext();

        IResult result = await scope.Handler.GetManifestAsync(
            context,
            ProductId,
            "truckmate-9.9.9-stable-1",
            EnvironmentName,
            CancellationToken.None);

        (int statusCode, string body) = await ExecuteAsync(result, context);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
        Assert.Equal(string.Empty, body);
    }

    /// <summary>FR-3 TR-5 TR-9 returns 304 when the client If-None-Match value matches the manifest ETag.</summary>
    [Fact]
    public async Task MatchingETagReturnsNotModified()
    {
        using DistributionTestScope scope = CreateScope();
        DistributionManifest manifest = await scope.RegisterManifestAsync();
        DefaultHttpContext context = scope.CreateContext();
        context.Request.Headers.IfNoneMatch = manifest.ETag;

        IResult result = await scope.Handler.GetManifestAsync(
            context,
            ProductId,
            ReleaseId,
            EnvironmentName,
            CancellationToken.None);

        (int statusCode, string body) = await ExecuteAsync(result, context);

        Assert.Equal(StatusCodes.Status304NotModified, statusCode);
        Assert.Equal(manifest.ETag, context.Response.Headers["ETag"]);
        Assert.Equal(string.Empty, body);
    }

    /// <summary>FR-8 TR-7 TR-9 buffers accepted exposure events in the DI-resolved in-memory store.</summary>
    [Fact]
    public async Task ExposureIngestionBuffersBatch()
    {
        using DistributionTestScope scope = CreateScope();
        DefaultHttpContext context = scope.CreateContext(body:
            """
            {
              "productId": "truckmate",
              "releaseId": "truckmate-1.2.3-stable-4",
              "environment": "Development",
              "events": [
                {
                  "flagKey": "new-dashboard",
                  "resolvedValue": true,
                  "matchedRuleIndex": 0,
                  "contextFingerprint": "ctx-1",
                  "timestamp": "2026-05-14T14:00:00Z"
                },
                {
                  "flagKey": "reports-title",
                  "resolvedValue": "Modern",
                  "matchedRuleIndex": null,
                  "contextFingerprint": "ctx-2",
                  "timestamp": "2026-05-14T14:00:01Z"
                }
              ]
            }
            """);

        IResult result = await scope.Handler.PostExposureAsync(context, CancellationToken.None);

        (int statusCode, string body) = await ExecuteAsync(result, context);
        IReadOnlyList<StoredExposureEvent> events = scope.ExposureStore.Snapshot();

        Assert.Equal(StatusCodes.Status202Accepted, statusCode);
        Assert.Equal("{\"accepted\":2}", body);
        Assert.Equal(2, scope.ExposureStore.Count);
        Assert.Equal(2, events.Count);
        Assert.Equal("new-dashboard", events[0].FlagKey);
        Assert.Equal(ProductId, events[0].ProductId);
    }

    /// <summary>TR-9 rejects Distribution requests that do not present a valid product-scoped API key.</summary>
    [Fact]
    public async Task InvalidProductApiKeyIsRejected()
    {
        using DistributionTestScope scope = CreateScope();
        await scope.RegisterManifestAsync();
        DefaultHttpContext context = scope.CreateContext(apiKey: "wrong-key");

        IResult result = await scope.Handler.GetManifestAsync(
            context,
            ProductId,
            ReleaseId,
            EnvironmentName,
            CancellationToken.None);

        (int statusCode, string body) = await ExecuteAsync(result, context);

        Assert.Equal(StatusCodes.Status401Unauthorized, statusCode);
        Assert.Equal(string.Empty, body);
    }

    private static DistributionTestScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharpNinjaFeatureFlagDistribution(static options =>
        {
            options.DefaultEnvironment = EnvironmentName;
            options.ProductApiKeys[ProductId] = [ApiKey];
        });

        return new DistributionTestScope(services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            }));
    }

    private static async Task<(int StatusCode, string Body)> ExecuteAsync(IResult result, DefaultHttpContext context)
    {
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;

        using var reader = new StreamReader(
            context.Response.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);

        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private sealed class DistributionTestScope : IDisposable
    {
        private readonly ServiceProvider serviceProvider;

        public DistributionTestScope(ServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public DistributionEndpointHandler Handler =>
            serviceProvider.GetRequiredService<DistributionEndpointHandler>();

        public IDistributionManifestRegistry ManifestRegistry =>
            serviceProvider.GetRequiredService<IDistributionManifestRegistry>();

        public IExposureEventStore ExposureStore =>
            serviceProvider.GetRequiredService<IExposureEventStore>();

        public async Task<DistributionManifest> RegisterManifestAsync()
        {
            var updatedAt = new DateTimeOffset(2026, 5, 14, 14, 0, 0, TimeSpan.Zero);
            DistributionManifest manifest = DistributionManifest.FromJson(ManifestJson, updatedAt);
            await ManifestRegistry.UpsertAsync(manifest, CancellationToken.None);
            return manifest;
        }

        public DefaultHttpContext CreateContext(string apiKey = ApiKey, string? body = null)
        {
            var context = new DefaultHttpContext
            {
                RequestServices = serviceProvider,
            };
            context.Response.Body = new MemoryStream();
            context.Request.Headers[SharpNinjaDistributionHeaders.ProductApiKeyHeaderName] = apiKey;

            if (body is not null)
            {
                context.Request.ContentType = "application/json";
                context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            }

            return context;
        }

        public void Dispose() => serviceProvider.Dispose();
    }
}
