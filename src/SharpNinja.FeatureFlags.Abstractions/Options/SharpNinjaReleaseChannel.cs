namespace SharpNinja.FeatureFlags.Abstractions.Options;

/// <summary>FR-1 v1 contract: release channel used with semantic version and build lineage.</summary>
public enum SharpNinjaReleaseChannel
{
    /// <summary>FR-1 v1 contract: canary release channel.</summary>
    Canary = 0,

    /// <summary>FR-1 v1 contract: beta release channel.</summary>
    Beta = 1,

    /// <summary>FR-1 v1 contract: stable release channel.</summary>
    Stable = 2,
}
