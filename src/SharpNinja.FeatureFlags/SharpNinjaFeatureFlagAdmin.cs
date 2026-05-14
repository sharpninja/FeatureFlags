using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;

namespace SharpNinja.FeatureFlags;

internal sealed class SharpNinjaFeatureFlagAdmin : ISharpNinjaFeatureFlagAdmin
{
    private readonly ISharpNinjaActiveManifestStore activeManifestStore;
    private readonly ISharpNinjaExposureEventBuffer exposureEventBuffer;
    private readonly ISharpNinjaExposureUploadCoordinator exposureUploadCoordinator;
    private readonly ISharpNinjaRemoteFetchCoordinator remoteFetchCoordinator;
    private readonly SharpNinjaFeatureFlagOptions options;

    public SharpNinjaFeatureFlagAdmin(
        ISharpNinjaActiveManifestStore activeManifestStore,
        ISharpNinjaRemoteFetchCoordinator remoteFetchCoordinator,
        ISharpNinjaExposureEventBuffer exposureEventBuffer,
        ISharpNinjaExposureUploadCoordinator exposureUploadCoordinator,
        SharpNinjaFeatureFlagOptions options)
    {
        this.activeManifestStore = activeManifestStore ?? throw new ArgumentNullException(nameof(activeManifestStore));
        this.remoteFetchCoordinator = remoteFetchCoordinator ?? throw new ArgumentNullException(nameof(remoteFetchCoordinator));
        this.exposureEventBuffer = exposureEventBuffer ?? throw new ArgumentNullException(nameof(exposureEventBuffer));
        this.exposureUploadCoordinator = exposureUploadCoordinator ?? throw new ArgumentNullException(nameof(exposureUploadCoordinator));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.activeManifestStore.ManifestUpdated += OnManifestUpdated;
    }

    public event EventHandler<ManifestUpdatedEventArgs>? ManifestUpdated;

    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        await remoteFetchCoordinator.RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ForceRefreshAsync(CancellationToken cancellationToken = default)
    {
        await remoteFetchCoordinator.ForceRefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public DiagnosticSnapshot GetDiagnostics()
    {
        ManifestRefreshResult refreshResult = remoteFetchCoordinator.LastRefreshResult;
        return new DiagnosticSnapshot(
            options.ProductId,
            options.ReleaseId,
            activeManifestStore.CurrentEnvelope.ManifestId,
            activeManifestStore.LastUpdated)
        {
            LastRefreshStatus = refreshResult.Status,
            LastRefreshError = refreshResult.ErrorMessage,
            PendingExposureCount = exposureEventBuffer.Snapshot().Count,
            LastExposureUpload = exposureUploadCoordinator.LastSuccessfulUpload,
        };
    }

    private void OnManifestUpdated(object? sender, ManifestUpdatedEventArgs eventArgs)
    {
        ManifestUpdated?.Invoke(this, eventArgs);
    }
}
