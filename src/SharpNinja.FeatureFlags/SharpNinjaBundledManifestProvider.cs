using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags;

internal sealed class SharpNinjaBundledManifestProvider : ISharpNinjaBundledManifestProvider
{
    private readonly SignedManifestEnvelope envelope;

    public SharpNinjaBundledManifestProvider(SignedManifestEnvelope envelope)
    {
        this.envelope = envelope?.Validate() ?? throw new ArgumentNullException(nameof(envelope));
    }

    public SignedManifestEnvelope GetBundledManifest() => envelope;
}
