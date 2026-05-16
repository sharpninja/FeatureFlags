using System.Security.Cryptography;
using System.Text;

namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-2 FR-3 TR-4 TR-6 TR-11 v1 signed manifest envelope carried by bundled, cache, and remote sources.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-2"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-3"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-4"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-6"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
/// <param name="ManifestJson">Canonical manifest JSON payload.</param>
/// <param name="Signature">Manifest signature text.</param>
/// <param name="SigningKeyId">Signing key identifier.</param>
/// <param name="Algorithm">Signature algorithm name.</param>
public sealed record SignedManifestEnvelope(
    string ManifestJson,
    string Signature,
    string SigningKeyId,
    string Algorithm)
{
    /// <summary>FR-2 FR-3 TR-4 v1 manifest identifier derived from the JSON payload.</summary>
    public string ManifestId { get; init; } = ComputeManifestId(ManifestJson);

    /// <summary>FR-3 TR-6 v1 optional distribution ETag for conditional refresh requests.</summary>
    public string? ETag { get; init; }

    /// <summary>FR-3 TR-10 v1 optional manifest publication timestamp.</summary>
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>FR-2 FR-3 TR-4 v1 validates the structural envelope fields before SDK use.</summary>
    /// <returns>The current envelope when validation succeeds.</returns>
    public SignedManifestEnvelope Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ManifestJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(Signature);
        ArgumentException.ThrowIfNullOrWhiteSpace(SigningKeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(Algorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(ManifestId);

        return this;
    }

    private static string ComputeManifestId(string manifestJson)
    {
        if (string.IsNullOrEmpty(manifestJson))
        {
            return string.Empty;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(manifestJson);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
