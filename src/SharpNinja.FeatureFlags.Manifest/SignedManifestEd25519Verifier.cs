using System.Text.Json;
using System.Text.Json.Nodes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace SharpNinja.FeatureFlags.Manifest;

/// <summary>Verifies a complete signed manifest using the keytool's Ed25519 canonical form.</summary>
public static class SignedManifestEd25519Verifier
{
    /// <summary>Returns true only when the signed JSON matches the trusted raw public key.</summary>
    public static bool Verify(string manifestJson, ReadOnlySpan<byte> publicKeyBytes)
    {
        if (string.IsNullOrWhiteSpace(manifestJson) || publicKeyBytes.Length != 32)
        {
            return false;
        }

        try
        {
            if (JsonNode.Parse(manifestJson) is not JsonObject root
                || root["signature"] is not JsonObject signature)
            {
                return false;
            }

            string? algorithm = signature["algorithm"]?.GetValue<string>();
            string? keyId = signature["keyId"]?.GetValue<string>();
            string? encoded = signature["value"]?.GetValue<string>();
            if (!string.Equals(algorithm, "Ed25519", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(keyId)
                || string.IsNullOrWhiteSpace(encoded))
            {
                return false;
            }

            byte[] signatureBytes = Convert.FromBase64String(encoded);
            if (signatureBytes.Length != 64)
            {
                return false;
            }

            signature["value"] = string.Empty;
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = true,
                SkipValidation = false,
            }))
            {
                root.WriteTo(writer);
            }

            byte[] canonical = stream.ToArray();
            var verifier = new Ed25519Signer();
            verifier.Init(forSigning: false, new Ed25519PublicKeyParameters(publicKeyBytes.ToArray(), 0));
            verifier.BlockUpdate(canonical, 0, canonical.Length);
            return verifier.VerifySignature(signatureBytes);
        }
        catch (Exception exception) when (exception is JsonException
            or FormatException
            or ArgumentException
            or InvalidOperationException)
        {
            return false;
        }
    }
}
