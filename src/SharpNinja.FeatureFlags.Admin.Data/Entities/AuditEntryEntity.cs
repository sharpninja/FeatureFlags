namespace SharpNinja.FeatureFlags.Admin.Data.Entities;

/// <summary>FR-9 FR-11 TR-9: EF Core entity persisting an <see cref="AdminAuditEntry"/> row.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-11"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// </remarks>
public sealed record class AuditEntryEntity
{
    /// <summary>Gets or sets the identity-assigned audit sequence number (primary key).</summary>
    public long Sequence { get; set; }

    /// <summary>Gets or sets the audit action type.</summary>
    public AdminAuditAction Action { get; set; }

    /// <summary>Gets or sets the feature flag key.</summary>
    public string FlagKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the primary environment name for the action.</summary>
    public string EnvironmentName { get; set; } = string.Empty;

    /// <summary>Gets or sets the target environment for promotion actions, or null.</summary>
    public string? TargetEnvironmentName { get; set; }

    /// <summary>Gets or sets the JSON-serialized product scope collection at the time of the action.</summary>
    public string ProductScopeJson { get; set; } = "[]";

    /// <summary>Gets or sets the feature flag value type at the time of the action.</summary>
    public string ValueType { get; set; } = string.Empty;

    /// <summary>Gets or sets the serialized default value at the time of the action.</summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON-serialized rule descriptions collection at the time of the action.</summary>
    public string RuleDescriptionsJson { get; set; } = "[]";

    /// <summary>Gets or sets the reason supplied by the actor.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the embedded RBAC metadata for the actor.</summary>
    public RbacMetadataOwned RbacMetadata { get; set; } = new();

    /// <summary>Gets or sets the draft revision at the time of the action.</summary>
    public long Revision { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the audit entry was recorded.</summary>
    public DateTimeOffset OccurredAt { get; set; }
}
