namespace SharpNinja.FeatureFlags.Abstractions.Options;

/// <summary>FR-1 v1 contract: release channel used with semantic version and build lineage.</summary>
/// <remarks>
/// Members carry stable ordinal values that consumers may persist; treat the enumeration as part of the public contract.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-1"/>
/// </remarks>
public enum SharpNinjaReleaseChannel
{
    /// <summary>FR-1 v1 contract: canary release channel.</summary>
    Canary = 0,

    /// <summary>FR-1 v1 contract: beta release channel.</summary>
    Beta = 1,

    /// <summary>FR-1 v1 contract: stable release channel.</summary>
    Stable = 2,
}
