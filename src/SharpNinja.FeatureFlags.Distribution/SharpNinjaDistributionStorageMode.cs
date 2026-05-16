namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-3 FR-8 v1 built-in Distribution storage modes.</summary>
/// <remarks>
/// Members carry stable ordinal values that consumers may persist; treat the enumeration as part of the public contract.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-3"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// </remarks>
public enum SharpNinjaDistributionStorageMode
{
    /// <summary>FR-3 FR-8 stores manifests and exposure events in process memory only.</summary>
    InMemory = 0,

    /// <summary>FR-3 FR-8 stores manifests and exposure events under a durable file-system root.</summary>
    FileSystem = 1,
}
