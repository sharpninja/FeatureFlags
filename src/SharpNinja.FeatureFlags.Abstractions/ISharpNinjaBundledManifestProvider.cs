namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-2 TR-4 TR-6 TR-11 v1 provider for the signed manifest bundled with an SDK consumer build.</summary>
/// <remarks>
/// v1 contract. Implementations are responsible for documenting their own thread-safety and lifecycle.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-2"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-4"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-6"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public interface ISharpNinjaBundledManifestProvider
{
    /// <summary>FR-2 returns the signed bundled manifest envelope.</summary>
    /// <returns>Bundled manifest envelope.</returns>
    SignedManifestEnvelope GetBundledManifest();
}
