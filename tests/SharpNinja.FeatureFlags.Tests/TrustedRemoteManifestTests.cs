using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;
using Xunit;

namespace SharpNinja.FeatureFlags.Tests;

/// <summary>TEST-GATEWAY-006: the SDK verifies a remote manifest against the trusted build key.</summary>
public sealed class TrustedRemoteManifestTests
{
    /// <summary>Trusted remote payloads activate only when their Ed25519 signature is intact.</summary>
    [Fact]
    public void ManifestVerifierAcceptsTrustedSignatureAndRejectsChangedPayload()
    {
        byte[] seed = Enumerable.Range(1, 32).Select(static value => (byte)value).ToArray();
        var privateKey = new Ed25519PrivateKeyParameters(seed, 0);
        const string bundled = """
            {
              "schemaVersion": 1,
              "productId": "truckmate",
              "releaseId": "2026.05",
              "environment": "Development",
              "flags": []
            }
            """;
        JsonObject signed = (JsonObject)JsonNode.Parse(bundled)!;
        var signature = new JsonObject
        {
            ["algorithm"] = "Ed25519",
            ["keyId"] = "test-key",
            ["value"] = string.Empty,
        };
        signed["signature"] = signature;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            signed.WriteTo(writer);
        }

        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, privateKey);
        byte[] canonical = stream.ToArray();
        signer.BlockUpdate(canonical, 0, canonical.Length);
        string encoded = Convert.ToBase64String(signer.GenerateSignature());
        signature["value"] = encoded;
        string validJson = signed.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        SignedManifestEnvelope valid = new(validJson, encoded, "test-key", "Ed25519");

        SharpNinjaFeatureFlagOptions options = new(
            "truckmate",
            "2026.05",
            "Development",
            TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(30))
        {
            ManifestPublicKey = privateKey.GeneratePublicKey().GetEncoded(),
        };
        var services = new ServiceCollection();
        services.AddSharpNinjaFeatureFlags(options, bundled);
        using ServiceProvider provider = services.BuildServiceProvider();
        ISharpNinjaManifestSignatureVerifier verifier =
            provider.GetRequiredService<ISharpNinjaManifestSignatureVerifier>();

        Assert.True(verifier.Verify(valid, out string? validError), validError);
        signed["flags"] = new JsonArray(new JsonObject { ["key"] = "tampered" });
        SignedManifestEnvelope changed = valid with
        {
            ManifestJson = signed.ToJsonString(),
        };
        Assert.False(verifier.Verify(changed, out _));
    }
}
