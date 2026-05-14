using Microsoft.Extensions.Logging;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;
using SharpNinja.FeatureFlags.Evaluation;

namespace SharpNinja.FeatureFlags;

internal sealed class SharpNinjaActiveManifestStore : ISharpNinjaActiveManifestStore
{
    private static readonly Action<ILogger, string, Exception?> ManifestCacheRejected =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1001, nameof(ManifestCacheRejected)),
            "Feature flag manifest cache was rejected: {VerificationError}");

    private static readonly Action<ILogger, string, Exception?> ManifestCacheActivationFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1002, nameof(ManifestCacheActivationFailed)),
            "Feature flag manifest cache could not be activated: {ActivationError}");

    private readonly Lock gate = new();
    private readonly ILogger<SharpNinjaActiveManifestStore> logger;
    private readonly SharpNinjaFeatureFlagOptions options;
    private readonly TimeProvider timeProvider;
    private SignedManifestEnvelope currentEnvelope;
    private FeatureFlagManifest currentManifest;
    private DateTimeOffset lastUpdated;

    public SharpNinjaActiveManifestStore(
        ISharpNinjaBundledManifestProvider bundledManifestProvider,
        ISharpNinjaManifestCacheStore manifestCacheStore,
        ISharpNinjaManifestSignatureVerifier signatureVerifier,
        SharpNinjaFeatureFlagOptions options,
        TimeProvider timeProvider,
        ILogger<SharpNinjaActiveManifestStore> logger)
    {
        ArgumentNullException.ThrowIfNull(bundledManifestProvider);
        ArgumentNullException.ThrowIfNull(manifestCacheStore);
        ArgumentNullException.ThrowIfNull(signatureVerifier);
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        SignedManifestEnvelope bundledEnvelope = bundledManifestProvider.GetBundledManifest().Validate();
        currentManifest = ParseAndValidate(bundledEnvelope, out string? bundledError)
            ?? throw new InvalidOperationException(bundledError ?? "Bundled manifest could not be parsed.");
        currentEnvelope = bundledEnvelope;
        lastUpdated = this.timeProvider.GetUtcNow();

        SignedManifestEnvelope? cachedEnvelope = manifestCacheStore.Read();
        if (cachedEnvelope is null)
        {
            return;
        }

        if (!signatureVerifier.Verify(cachedEnvelope, out string? verificationError))
        {
            ManifestCacheRejected(logger, verificationError ?? "Unknown verification error.", null);
            return;
        }

        if (!TryActivate(cachedEnvelope, out string? activationError))
        {
            ManifestCacheActivationFailed(logger, activationError ?? "Unknown activation error.", null);
        }
    }

    public event EventHandler<ManifestUpdatedEventArgs>? ManifestUpdated;

    public FeatureFlagManifest CurrentManifest
    {
        get
        {
            lock (gate)
            {
                return currentManifest;
            }
        }
    }

    public SignedManifestEnvelope CurrentEnvelope
    {
        get
        {
            lock (gate)
            {
                return currentEnvelope;
            }
        }
    }

    public DateTimeOffset LastUpdated
    {
        get
        {
            lock (gate)
            {
                return lastUpdated;
            }
        }
    }

    public bool TryActivate(SignedManifestEnvelope envelope, out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        FeatureFlagManifest? parsedManifest = ParseAndValidate(envelope, out errorMessage);
        if (parsedManifest is null)
        {
            return false;
        }

        DateTimeOffset updatedAt = timeProvider.GetUtcNow();
        bool changed;
        lock (gate)
        {
            changed = !string.Equals(currentEnvelope.ManifestId, envelope.ManifestId, StringComparison.Ordinal);
            currentEnvelope = envelope;
            currentManifest = parsedManifest;
            lastUpdated = updatedAt;
        }

        if (changed)
        {
            ManifestUpdated?.Invoke(this, new ManifestUpdatedEventArgs(envelope.ManifestId, updatedAt));
        }

        return true;
    }

    private FeatureFlagManifest? ParseAndValidate(SignedManifestEnvelope envelope, out string? errorMessage)
    {
        try
        {
            envelope.Validate();
            FeatureFlagManifest manifest = FeatureFlagManifest.Parse(envelope.ManifestJson);

            if (!string.Equals(manifest.ProductId, options.ProductId, StringComparison.Ordinal))
            {
                errorMessage = $"Manifest productId '{manifest.ProductId}' does not match configured product '{options.ProductId}'.";
                return null;
            }

            if (!string.Equals(manifest.ReleaseId, options.ReleaseId, StringComparison.Ordinal))
            {
                errorMessage = $"Manifest releaseId '{manifest.ReleaseId}' does not match configured release '{options.ReleaseId}'.";
                return null;
            }

            if (!string.Equals(manifest.Environment, options.Environment, StringComparison.Ordinal))
            {
                errorMessage = $"Manifest environment '{manifest.Environment}' does not match configured environment '{options.Environment}'.";
                return null;
            }

            errorMessage = null;
            return manifest;
        }
        catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException)
        {
            errorMessage = exception.Message;
            return null;
        }
    }
}
