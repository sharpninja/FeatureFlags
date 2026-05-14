using System.Text;
using Microsoft.Extensions.Configuration;
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
    private const string AttestationToken = "test-attestation-token";

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

    /// <summary>TR-9 validates request authorization that composes product API keys and device-attestation policy.</summary>
    [Fact]
    public async Task ManifestLookupRequiresDeviceAttestationWhenPolicyRequiresIt()
    {
        using DistributionTestScope scope = CreateScope(options =>
        {
            options.RequireDeviceAttestation = true;
            options.DeviceAttestationTestTokens[ProductId] = [AttestationToken];
        });
        await scope.RegisterManifestAsync();
        DefaultHttpContext missingAttestationContext = scope.CreateContext();

        IResult missingResult = await scope.Handler.GetManifestAsync(
            missingAttestationContext,
            ProductId,
            ReleaseId,
            EnvironmentName,
            CancellationToken.None);

        (int missingStatusCode, string missingBody) = await ExecuteAsync(missingResult, missingAttestationContext);

        Assert.Equal(StatusCodes.Status403Forbidden, missingStatusCode);
        Assert.Equal("{\"error\":\"missing_device_attestation\"}", missingBody);

        DefaultHttpContext attestedContext = scope.CreateContext(deviceAttestationToken: AttestationToken);

        IResult attestedResult = await scope.Handler.GetManifestAsync(
            attestedContext,
            ProductId,
            ReleaseId,
            EnvironmentName,
            CancellationToken.None);

        (int attestedStatusCode, string attestedBody) = await ExecuteAsync(attestedResult, attestedContext);

        Assert.Equal(StatusCodes.Status200OK, attestedStatusCode);
        Assert.Contains("\"productId\": \"truckmate\"", attestedBody, StringComparison.Ordinal);
    }

    /// <summary>TR-10 exposes Prometheus counters for auth, attestation, cache, and exposure activity.</summary>
    [Fact]
    public async Task MetricsIncludeAuthAttestationCacheAndExposureCounters()
    {
        using DistributionTestScope scope = CreateScope(options =>
        {
            options.RequireDeviceAttestation = true;
            options.DeviceAttestationTestTokens[ProductId] = [AttestationToken];
        });
        await scope.RegisterManifestAsync();

        IResult hitResult = await scope.Handler.GetManifestAsync(
            scope.CreateContext(deviceAttestationToken: AttestationToken),
            ProductId,
            ReleaseId,
            EnvironmentName,
            CancellationToken.None);
        await ExecuteAsync(hitResult, scope.LastContext);

        IResult missingAttestationResult = await scope.Handler.GetManifestAsync(
            scope.CreateContext(),
            ProductId,
            ReleaseId,
            EnvironmentName,
            CancellationToken.None);
        await ExecuteAsync(missingAttestationResult, scope.LastContext);

        IResult invalidKeyResult = await scope.Handler.GetManifestAsync(
            scope.CreateContext(apiKey: "wrong-key"),
            ProductId,
            ReleaseId,
            EnvironmentName,
            CancellationToken.None);
        await ExecuteAsync(invalidKeyResult, scope.LastContext);

        IResult missResult = await scope.Handler.GetManifestAsync(
            scope.CreateContext(deviceAttestationToken: AttestationToken),
            ProductId,
            "truckmate-9.9.9-stable-1",
            EnvironmentName,
            CancellationToken.None);
        await ExecuteAsync(missResult, scope.LastContext);

        IResult exposureResult = await scope.Handler.PostExposureAsync(
            scope.CreateContext(
                deviceAttestationToken: AttestationToken,
                body:
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
                    }
                  ]
                }
                """),
            CancellationToken.None);
        await ExecuteAsync(exposureResult, scope.LastContext);

        DefaultHttpContext metricsContext = scope.CreateContext(deviceAttestationToken: AttestationToken);
        IResult metricsResult = scope.Handler.GetMetrics();

        (int statusCode, string metrics) = await ExecuteAsync(metricsResult, metricsContext);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Contains("sharpninja_distribution_auth_success_total 4", metrics, StringComparison.Ordinal);
        Assert.Contains("sharpninja_distribution_auth_failure_total 1", metrics, StringComparison.Ordinal);
        Assert.Contains("sharpninja_distribution_attestation_success_total 3", metrics, StringComparison.Ordinal);
        Assert.Contains("sharpninja_distribution_attestation_failure_total 1", metrics, StringComparison.Ordinal);
        Assert.Contains("sharpninja_distribution_manifest_cache_hits_total 1", metrics, StringComparison.Ordinal);
        Assert.Contains("sharpninja_distribution_manifest_cache_misses_total 1", metrics, StringComparison.Ordinal);
        Assert.Contains("sharpninja_distribution_exposure_batches_total 1", metrics, StringComparison.Ordinal);
        Assert.Contains("sharpninja_distribution_exposure_events_total 1", metrics, StringComparison.Ordinal);
    }

    /// <summary>FR-3 FR-8 verifies the file-backed manifest and exposure stores reload persisted data.</summary>
    [Fact]
    public async Task FileBackedStoresReloadPersistedManifestAndExposureEvents()
    {
        string storageRoot = Path.Combine(Path.GetTempPath(), "sharpninja-distribution-tests", Guid.NewGuid().ToString("N"));
        try
        {
            using (DistributionTestScope firstScope = CreateScope(options =>
            {
                options.StorageMode = SharpNinjaDistributionStorageMode.FileSystem;
                options.StorageRootPath = storageRoot;
            }))
            {
                await firstScope.RegisterManifestAsync();
                IResult result = await firstScope.Handler.PostExposureAsync(
                    firstScope.CreateContext(body:
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
                        }
                      ]
                    }
                    """),
                    CancellationToken.None);
                await ExecuteAsync(result, firstScope.LastContext);
            }

            using DistributionTestScope secondScope = CreateScope(options =>
            {
                options.StorageMode = SharpNinjaDistributionStorageMode.FileSystem;
                options.StorageRootPath = storageRoot;
            });

            DistributionManifest? manifest = await secondScope.ManifestRegistry.FindAsync(
                ProductId,
                ReleaseId,
                EnvironmentName,
                CancellationToken.None);

            Assert.NotNull(manifest);
            Assert.Equal(1, secondScope.ManifestRegistry.Count);
            Assert.Equal(1, secondScope.ExposureStore.Count);
            Assert.Equal("new-dashboard", Assert.Single(secondScope.ExposureStore.Snapshot()).FlagKey);
        }
        finally
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }
    }

    /// <summary>FR-3 TR-9 verifies configuration binding can select durable storage and attestation policy.</summary>
    [Fact]
    public void ConfigurationSectionRegistersFileBackedAttestationReadyServices()
    {
        string storageRoot = Path.Combine(Path.GetTempPath(), "sharpninja-distribution-tests", Guid.NewGuid().ToString("N"));
        var values = new Dictionary<string, string?>
        {
            ["DefaultEnvironment"] = EnvironmentName,
            ["Storage:Mode"] = "FileSystem",
            ["Storage:RootPath"] = storageRoot,
            ["Authorization:RequireDeviceAttestation"] = "true",
            ["ApiKeys:truckmate:0"] = ApiKey,
            ["DeviceAttestation:TestTokens:truckmate:0"] = AttestationToken,
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharpNinjaFeatureFlagDistribution(configuration);

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        Assert.IsType<FileBackedDistributionManifestRegistry>(
            provider.GetRequiredService<IDistributionManifestRegistry>());
        Assert.IsType<FileBackedExposureEventStore>(
            provider.GetRequiredService<IExposureEventStore>());
    }

    /// <summary>FR-3 FR-8 TR-9 verifies docker-compose carries Distribution deployment-ready configuration.</summary>
    [Fact]
    public void DockerComposeIncludesDistributionDeploymentConfiguration()
    {
        string repositoryRoot = FindRepositoryRoot();
        string compose = File.ReadAllText(Path.Combine(repositoryRoot, "docker-compose.yml"));

        Assert.Contains("Distribution__Storage__Mode: FileSystem", compose, StringComparison.Ordinal);
        Assert.Contains("Distribution__Storage__RootPath: /data/distribution", compose, StringComparison.Ordinal);
        Assert.Contains("Distribution__Cdn__ManifestStaleWhileRevalidateSeconds: \"300\"", compose, StringComparison.Ordinal);
        Assert.Contains("Distribution__Authorization__RequireDeviceAttestation: \"true\"", compose, StringComparison.Ordinal);
        Assert.Contains("Distribution__ApiKeys__TruckMate__0:", compose, StringComparison.Ordinal);
        Assert.Contains("Distribution__DeviceAttestation__TestTokens__TruckMate__0:", compose, StringComparison.Ordinal);
        Assert.Contains("featureflags-distribution:/data/distribution", compose, StringComparison.Ordinal);
        Assert.Contains("featureflags-distribution:", compose, StringComparison.Ordinal);
    }

    private static DistributionTestScope CreateScope(Action<SharpNinjaDistributionBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharpNinjaFeatureFlagDistribution(options =>
        {
            options.DefaultEnvironment = EnvironmentName;
            options.ProductApiKeys[ProductId] = [ApiKey];
            configure?.Invoke(options);
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

        public DefaultHttpContext LastContext { get; private set; } = new();

        public async Task<DistributionManifest> RegisterManifestAsync()
        {
            var updatedAt = new DateTimeOffset(2026, 5, 14, 14, 0, 0, TimeSpan.Zero);
            DistributionManifest manifest = DistributionManifest.FromJson(ManifestJson, updatedAt);
            await ManifestRegistry.UpsertAsync(manifest, CancellationToken.None);
            return manifest;
        }

        public DefaultHttpContext CreateContext(
            string apiKey = ApiKey,
            string? deviceAttestationToken = null,
            string? body = null)
        {
            var context = new DefaultHttpContext
            {
                RequestServices = serviceProvider,
            };
            context.Response.Body = new MemoryStream();
            context.Request.Headers[SharpNinjaDistributionHeaders.ProductApiKeyHeaderName] = apiKey;
            context.Request.Headers[SharpNinjaDistributionHeaders.DevicePlatformHeaderName] = "test";

            if (!string.IsNullOrWhiteSpace(deviceAttestationToken))
            {
                context.Request.Headers[SharpNinjaDistributionHeaders.DeviceAttestationTokenHeaderName] = deviceAttestationToken;
            }

            if (body is not null)
            {
                context.Request.ContentType = "application/json";
                context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            }

            LastContext = context;
            return context;
        }

        public void Dispose() => serviceProvider.Dispose();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "sharpninja-feature-flags.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
