using Microsoft.Extensions.Logging;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;

namespace SharpNinja.FeatureFlags;

internal sealed class SharpNinjaExposureUploadCoordinator : ISharpNinjaExposureUploadCoordinator
{
    private static readonly Action<ILogger, Exception?> ExposureUploadFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1006, nameof(ExposureUploadFailed)),
            "Feature flag exposure upload failed.");

    private readonly ISharpNinjaExposureOutbox exposureOutbox;
    private readonly ISharpNinjaExposureUploader exposureUploader;
    private readonly Lock gate = new();
    private readonly ILogger<SharpNinjaExposureUploadCoordinator> logger;
    private readonly SharpNinjaFeatureFlagOptions options;
    private readonly TimeProvider timeProvider;
    private DateTimeOffset? lastUploadAttempt;
    private DateTimeOffset? lastSuccessfulUpload;

    public SharpNinjaExposureUploadCoordinator(
        ISharpNinjaExposureOutbox exposureOutbox,
        ISharpNinjaExposureUploader exposureUploader,
        SharpNinjaFeatureFlagOptions options,
        TimeProvider timeProvider,
        ILogger<SharpNinjaExposureUploadCoordinator> logger)
    {
        this.exposureOutbox = exposureOutbox ?? throw new ArgumentNullException(nameof(exposureOutbox));
        this.exposureUploader = exposureUploader ?? throw new ArgumentNullException(nameof(exposureUploader));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public DateTimeOffset? LastSuccessfulUpload
    {
        get
        {
            lock (gate)
            {
                return lastSuccessfulUpload;
            }
        }
    }

    public async ValueTask<SharpNinjaExposureUploadResult> FlushAsync(
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        lock (gate)
        {
            if (!force
                && lastUploadAttempt is DateTimeOffset lastAttempt
                && now - lastAttempt < options.ExposureUploadInterval)
            {
                return new SharpNinjaExposureUploadResult(0, SkippedByCadence: true);
            }

            lastUploadAttempt = now;
        }

        IReadOnlyList<SharpNinjaExposureEvent> batch = exposureOutbox.DequeueBatch(options.ExposureUploadBatchSize);
        if (batch.Count == 0)
        {
            return new SharpNinjaExposureUploadResult(0);
        }

        try
        {
            await exposureUploader.UploadAsync(batch, cancellationToken).ConfigureAwait(false);
            lock (gate)
            {
                lastSuccessfulUpload = now;
            }

            return new SharpNinjaExposureUploadResult(batch.Count);
        }
        catch (OperationCanceledException)
        {
            exposureOutbox.Requeue(batch);
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException)
        {
            exposureOutbox.Requeue(batch);
            ExposureUploadFailed(logger, exception);
            return new SharpNinjaExposureUploadResult(batch.Count, ErrorMessage: exception.Message);
        }
    }
}
