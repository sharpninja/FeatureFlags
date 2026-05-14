using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags;

internal sealed class SharpNinjaStructuralManifestSignatureVerifier : ISharpNinjaManifestSignatureVerifier
{
    public bool Verify(SignedManifestEnvelope envelope, out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        try
        {
            envelope.Validate();
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
