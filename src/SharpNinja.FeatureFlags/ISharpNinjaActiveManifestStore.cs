using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Evaluation;

namespace SharpNinja.FeatureFlags;

internal interface ISharpNinjaActiveManifestStore
{
    event EventHandler<ManifestUpdatedEventArgs>? ManifestUpdated;

    FeatureFlagManifest CurrentManifest { get; }

    SignedManifestEnvelope CurrentEnvelope { get; }

    DateTimeOffset LastUpdated { get; }

    bool TryActivate(SignedManifestEnvelope envelope, out string? errorMessage);
}
