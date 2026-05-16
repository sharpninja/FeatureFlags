namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-3 TR-6 TR-11 v1 durable store for the last verified signed manifest envelope.</summary>
/// <remarks>
/// v1 contract. Implementations are responsible for documenting their own thread-safety and lifecycle.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-3"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-6"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public interface ISharpNinjaManifestCacheStore
{
    /// <summary>TR-6 reads the cached signed manifest envelope loaded at SDK startup.</summary>
    /// <returns>The cached envelope, or <see langword="null" /> when no usable cache exists.</returns>
    SignedManifestEnvelope? Read();

    /// <summary>TR-6 persists the last verified signed manifest envelope.</summary>
    /// <param name="envelope">Envelope to persist.</param>
    void Write(SignedManifestEnvelope envelope);
}
