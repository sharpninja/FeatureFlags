using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;
using SharpNinja.FeatureFlags.Evaluation;
using Xunit;

namespace SharpNinja.FeatureFlags.Tests;

/// <summary>RELEASE-V1-AUDIT-001 follow-up tests for SDK manifest versioning, unauthorized-read warnings, and diagnostic snapshots.</summary>
public sealed class SharpNinjaFeatureClientAuditTests
{
    /// <summary>TR-8 verifies the SDK rejects a remote manifest with a schemaVersion higher than the SDK supports and retains the bundled manifest.</summary>
    [Fact]
    public async Task ForceRefreshRejectsManifestWithFutureSchemaVersionAndRetainsBundledManifest()
    {
        SharpNinjaFeatureFlagOptions options = CreateOptions();
        var remoteClient = new StubRemoteManifestClient(CreateEnvelope(FutureSchemaVersionManifestJson));

        using ServiceProvider provider = CreateProvider(
            options,
            services => services.AddSingleton<ISharpNinjaRemoteManifestClient>(remoteClient));
        ISharpNinjaFeatureFlagAdmin admin = provider.GetRequiredService<ISharpNinjaFeatureFlagAdmin>();
        ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();

        await admin.ForceRefreshAsync();
        DiagnosticSnapshot diagnostics = admin.GetDiagnostics();
        EvaluationResult<bool> postRefresh = client.Evaluate("audit.flag", false);

        Assert.Equal(ManifestRefreshStatus.Rejected, diagnostics.LastRefreshStatus);
        Assert.NotNull(diagnostics.LastRefreshError);
        Assert.Contains("schemaVersion", diagnostics.LastRefreshError, StringComparison.Ordinal);
        Assert.True(postRefresh.Value);
    }

    /// <summary>FR-10 verifies an evaluation against a flag whose productScope excludes the configured product emits a Warning-level log entry referencing the unauthorized read.</summary>
    [Fact]
    public void EvaluateEmitsWarningWhenProductScopeDoesNotIncludeConfiguredProduct()
    {
        SharpNinjaFeatureFlagOptions options = CreateOptions() with { SupportedProductIds = ["truckmate", "dispatch"] };
        var capture = new CapturedLogStore();

        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton(capture)
            .AddSingleton(typeof(ILogger<>), typeof(CapturingTypedLogger<>))
            .AddSharpNinjaFeatureFlags(options, ScopedManifestJson)
            .BuildServiceProvider();
        ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();

        EvaluationResult<bool> result = client.Evaluate("dispatch.only", defaultValue: false);

        Assert.False(result.Value);
        Assert.Equal(EvaluationReason.Default, result.Reason);
        Assert.Contains(
            capture.Entries,
            entry => entry.Level == LogLevel.Warning
                && entry.Message.Contains("dispatch.only", StringComparison.Ordinal)
                && entry.Message.Contains("truckmate", StringComparison.Ordinal));
    }

    /// <summary>TR-10 verifies the diagnostic snapshot exposes manifest identity, refresh status, error state, and exposure counters after a refresh cycle.</summary>
    [Fact]
    public async Task GetDiagnosticsReturnsActiveManifestStateAndRefreshOutcomeAfterRefresh()
    {
        DateTimeOffset start = new(2026, 5, 16, 9, 0, 0, TimeSpan.Zero);
        SharpNinjaFeatureFlagOptions options = CreateOptions();
        var remoteClient = new StubRemoteManifestClient(CreateEnvelope(RemoteManifestJson));
        var timeProvider = new FixedTimeProvider(start);

        using ServiceProvider provider = CreateProvider(
            options,
            services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
                services.AddSingleton<ISharpNinjaRemoteManifestClient>(remoteClient);
            });
        ISharpNinjaFeatureFlagAdmin admin = provider.GetRequiredService<ISharpNinjaFeatureFlagAdmin>();
        ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();

        await admin.ForceRefreshAsync();
        client.Evaluate("audit.flag", false);
        DiagnosticSnapshot diagnostics = admin.GetDiagnostics();
        TimeSpan cacheAge = timeProvider.GetUtcNow() - (diagnostics.LastUpdated ?? DateTimeOffset.MinValue);

        Assert.Equal(options.ProductId, diagnostics.ProductId);
        Assert.Equal(options.ReleaseId, diagnostics.ReleaseId);
        Assert.False(string.IsNullOrWhiteSpace(diagnostics.ManifestId));
        Assert.NotNull(diagnostics.LastUpdated);
        Assert.True(cacheAge >= TimeSpan.Zero);
        Assert.Equal(ManifestRefreshStatus.Updated, diagnostics.LastRefreshStatus);
        Assert.Null(diagnostics.LastRefreshError);
        Assert.True(diagnostics.PendingExposureCount >= 1);
    }

    private static ServiceProvider CreateProvider(
        SharpNinjaFeatureFlagOptions options,
        Action<IServiceCollection>? configureServices = null,
        string manifestJson = BundledManifestJson)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        return services
            .AddSharpNinjaFeatureFlags(options, manifestJson)
            .BuildServiceProvider();
    }

    private static SharpNinjaFeatureFlagOptions CreateOptions() =>
        new(
            ProductId: "truckmate",
            ReleaseId: "2026.05",
            Environment: "Development",
            ManifestRefreshInterval: TimeSpan.FromMinutes(5),
            ExposureUploadInterval: TimeSpan.FromSeconds(30))
        {
            ManifestCachePath = Path.Combine(CreateTempDirectory(), "manifest-cache.json"),
            ExposureOutboxPath = Path.Combine(CreateTempDirectory(), "exposure-outbox.json"),
        };

    private static SignedManifestEnvelope CreateEnvelope(string manifestJson) =>
        new(
            manifestJson,
            "test-signature",
            "test-key",
            "structural");

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "SharpNinja.FeatureFlags.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private const string BundledManifestJson = """
        {
          "schemaVersion": 1,
          "productId": "truckmate",
          "releaseId": "2026.05",
          "environment": "Development",
          "flags": [
            {
              "key": "audit.flag",
              "type": "boolean",
              "defaultValue": true,
              "killable": true,
              "productScope": [ "truckmate" ]
            }
          ]
        }
        """;

    private const string RemoteManifestJson = """
        {
          "schemaVersion": 1,
          "productId": "truckmate",
          "releaseId": "2026.05",
          "environment": "Development",
          "flags": [
            {
              "key": "audit.flag",
              "type": "boolean",
              "defaultValue": false,
              "killable": true,
              "productScope": [ "truckmate" ]
            }
          ]
        }
        """;

    private const string FutureSchemaVersionManifestJson = """
        {
          "schemaVersion": 2,
          "productId": "truckmate",
          "releaseId": "2026.05",
          "environment": "Development",
          "flags": [
            {
              "key": "audit.flag",
              "type": "boolean",
              "defaultValue": false,
              "killable": true,
              "productScope": [ "truckmate" ]
            }
          ]
        }
        """;

    private const string ScopedManifestJson = """
        {
          "schemaVersion": 1,
          "productId": "truckmate",
          "releaseId": "2026.05",
          "environment": "Development",
          "flags": [
            {
              "key": "dispatch.only",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "dispatch" ]
            }
          ]
        }
        """;

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubRemoteManifestClient(SignedManifestEnvelope envelope) : ISharpNinjaRemoteManifestClient
    {
        public ValueTask<RemoteManifestFetchResult> FetchAsync(
            SharpNinjaFeatureFlagOptions options,
            bool forceRefresh,
            string? currentETag,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            return ValueTask.FromResult(new RemoteManifestFetchResult(envelope));
        }
    }

    private sealed record CapturedLogEntry(LogLevel Level, EventId EventId, string Message, string CategoryName);

    private sealed class CapturedLogStore
    {
        public List<CapturedLogEntry> Entries { get; } = [];
    }

    private sealed class CapturingTypedLogger<T>(CapturedLogStore store) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            string category = typeof(T).FullName ?? typeof(T).Name;
            lock (store.Entries)
            {
                store.Entries.Add(new CapturedLogEntry(logLevel, eventId, formatter(state, exception), category));
            }
        }
    }
}
