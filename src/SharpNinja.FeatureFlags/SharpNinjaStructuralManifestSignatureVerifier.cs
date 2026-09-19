using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;
using SharpNinja.FeatureFlags.Manifest;

namespace SharpNinja.FeatureFlags;

internal sealed class SharpNinjaStructuralManifestSignatureVerifier(
    SharpNinjaFeatureFlagOptions options) : ISharpNinjaManifestSignatureVerifier
{
    public bool Verify(SignedManifestEnvelope envelope, out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        try
        {
            envelope.Validate();
            if (!options.ManifestPublicKey.IsEmpty
                && !SignedManifestEd25519Verifier.Verify(
                    envelope.ManifestJson, options.ManifestPublicKey.Span))
            {
                errorMessage = "Manifest signature does not match the trusted public key.";
                return false;
            }

            errorMessage = null;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            errorMessage = exception.Message;
            return false;
        }
    }
}
