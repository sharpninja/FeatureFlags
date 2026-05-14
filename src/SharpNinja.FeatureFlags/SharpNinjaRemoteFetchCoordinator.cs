using Microsoft.Extensions.Logging;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;

namespace SharpNinja.FeatureFlags;

internal sealed class SharpNinjaRemoteFetchCoordinator : ISharpNinjaRemoteFetchCoordinator
{
    private static readonly Action<ILogger, Exception?> ManifestRefreshFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1004, nameof(ManifestRefreshFailed)),
            "Feature flag manifest refresh failed.");

    private readonly ISharpNinjaActiveManifestStore activeManifestStore;
    private readonly ISharpNinjaManifestCacheStore manifestCacheStore;
    private readonly ISharpNinjaManifestSignatureVerifier signatureVerifier;
    private readonly ISharpNinjaRemoteManifestClient remoteManifestClient;
    private readonly SharpNinjaFeatureFlagOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SharpNinjaRemoteFetchCoordinator> logger;
    private readonly Lock gate = new();
    private ManifestRefreshResult lastRefreshResult = new(ManifestRefreshStatus.NotConfigured);

    public SharpNinjaRemoteFetchCoordinator(
        ISharpNinjaActiveManifestStore activeManifestStore,
        ISharpNinjaManifestCacheStore manifestCacheStore,
        ISharpNinjaManifestSignatureVerifier signatureVerifier,
        ISharpNinjaRemoteManifestClient remoteManifestClient,
        SharpNinjaFeatureFlagOptions options,
        TimeProvider timeProvider,
        ILogger<SharpNinjaRemoteFetchCoordinator> logger)
    {
        this.activeManifestStore = activeManifestStore ?? throw new ArgumentNullException(nameof(activeManifestStore));
        this.manifestCacheStore = manifestCacheStore ?? throw new ArgumentNullException(nameof(manifestCacheStore));
        this.signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        this.remoteManifestClient = remoteManifestClient ?? throw new ArgumentNullException(nameof(remoteManifestClient));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ManifestRefreshResult LastRefreshResult
    {
        get
        {
            lock (gate)
            {
                return lastRefreshResult;
            }
        }
    }

    public ValueTask<ManifestRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
        RefreshCoreAsync(forceRefresh: false, cancellationToken);

    public ValueTask<ManifestRefreshResult> ForceRefreshAsync(CancellationToken cancellationToken = default) =>
        RefreshCoreAsync(forceRefresh: true, cancellationToken);

    private async ValueTask<ManifestRefreshResult> RefreshCoreAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        try
        {
            RemoteManifestFetchResult fetchResult = await remoteManifestClient
                .FetchAsync(options, forceRefresh, activeManifestStore.CurrentEnvelope.ETag, cancellationToken)
                .ConfigureAwait(false);

            ManifestRefreshResult refreshResult = ApplyFetchResult(fetchResult);
            RecordResult(refreshResult);
            return refreshResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
        {
            ManifestRefreshFailed(logger, exception);
            ManifestRefreshResult refreshResult = new(
                ManifestRefreshStatus.Failed,
                activeManifestStore.CurrentEnvelope.ManifestId,
                exception.Message,
                timeProvider.GetUtcNow());
            RecordResult(refreshResult);
            return refreshResult;
        }
    }

    private ManifestRefreshResult ApplyFetchResult(RemoteManifestFetchResult fetchResult)
    {
        DateTimeOffset refreshedAt = timeProvider.GetUtcNow();
        if (fetchResult.NotConfigured)
        {
            return new ManifestRefreshResult(
                ManifestRefreshStatus.NotConfigured,
                activeManifestStore.CurrentEnvelope.ManifestId,
                fetchResult.ErrorMessage,
                refreshedAt);
        }

        if (fetchResult.NotModified)
        {
            return new ManifestRefreshResult(
                ManifestRefreshStatus.Unchanged,
                activeManifestStore.CurrentEnvelope.ManifestId,
                RefreshedAt: refreshedAt);
        }

        if (fetchResult.Envelope is null)
        {
            return new ManifestRefreshResult(
                ManifestRefreshStatus.Failed,
                activeManifestStore.CurrentEnvelope.ManifestId,
                fetchResult.ErrorMessage ?? "Remote manifest response did not include an envelope.",
                refreshedAt);
        }

        if (!signatureVerifier.Verify(fetchResult.Envelope, out string? verificationError))
        {
            return new ManifestRefreshResult(
                ManifestRefreshStatus.Rejected,
                fetchResult.Envelope.ManifestId,
                verificationError,
                refreshedAt);
        }

        string previousManifestId = activeManifestStore.CurrentEnvelope.ManifestId;
        if (!activeManifestStore.TryActivate(fetchResult.Envelope, out string? activationError))
        {
            return new ManifestRefreshResult(
                ManifestRefreshStatus.Rejected,
                fetchResult.Envelope.ManifestId,
                activationError,
                refreshedAt);
        }

        manifestCacheStore.Write(fetchResult.Envelope);
        ManifestRefreshStatus status = !string.Equals(
            previousManifestId,
            fetchResult.Envelope.ManifestId,
            StringComparison.Ordinal)
            ? ManifestRefreshStatus.Updated
            : ManifestRefreshStatus.Unchanged;

        return new ManifestRefreshResult(status, fetchResult.Envelope.ManifestId, RefreshedAt: refreshedAt);
    }

    private void RecordResult(ManifestRefreshResult refreshResult)
    {
        lock (gate)
        {
            lastRefreshResult = refreshResult;
        }
    }
}
