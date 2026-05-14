namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-3 FR-8 v1 built-in Distribution storage modes.</summary>
public enum SharpNinjaDistributionStorageMode
{
    /// <summary>FR-3 FR-8 stores manifests and exposure events in process memory only.</summary>
    InMemory = 0,

    /// <summary>FR-3 FR-8 stores manifests and exposure events under a durable file-system root.</summary>
    FileSystem = 1,
}
