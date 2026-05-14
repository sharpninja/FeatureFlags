namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-2 TR-4 TR-6 TR-11 v1 provider for the signed manifest bundled with an SDK consumer build.</summary>
public interface ISharpNinjaBundledManifestProvider
{
    /// <summary>FR-2 returns the signed bundled manifest envelope.</summary>
    /// <returns>Bundled manifest envelope.</returns>
    SignedManifestEnvelope GetBundledManifest();
}
