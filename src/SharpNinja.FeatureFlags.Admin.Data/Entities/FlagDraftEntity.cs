namespace SharpNinja.FeatureFlags.Admin.Data.Entities;

/// <summary>FR-9 FR-10 FR-11 TR-9: EF Core entity persisting a <see cref="SharpNinja.FeatureFlags.Admin.FeatureFlagDraft"/> row.</summary>
public sealed record class FlagDraftEntity
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the feature flag key.</summary>
    public string FlagKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the environment name.</summary>
    public string EnvironmentName { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON-serialized product scope collection.</summary>
    public string ProductScopeJson { get; set; } = "[]";

    /// <summary>Gets or sets the feature flag value type.</summary>
    public string ValueType { get; set; } = string.Empty;

    /// <summary>Gets or sets the serialized default value.</summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON-serialized rule descriptions collection.</summary>
    public string RuleDescriptionsJson { get; set; } = "[]";

    /// <summary>Gets or sets the most recent authoring reason.</summary>
    public string LastReason { get; set; } = string.Empty;

    /// <summary>Gets or sets the embedded RBAC metadata from the latest mutation.</summary>
    public RbacMetadataOwned LastRbacMetadata { get; set; } = new();

    /// <summary>Gets or sets the monotonic draft revision counter.</summary>
    public long Revision { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the latest mutation.</summary>
    public DateTimeOffset LastModifiedAt { get; set; }
}
