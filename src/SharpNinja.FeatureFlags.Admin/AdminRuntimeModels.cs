namespace SharpNinja.FeatureFlags.Admin;

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-11: Admin audit action types recorded by the runtime.</summary>
public enum AdminAuditAction
{
    /// <summary>FR-9: a flag draft was created.</summary>
    Created,

    /// <summary>FR-9: an existing flag draft was updated.</summary>
    Updated,

    /// <summary>FR-9 FR-11: a flag draft was published for an environment.</summary>
    Published,

    /// <summary>FR-9 FR-11: a flag draft was promoted between environments.</summary>
    Promoted,
}

/// <summary>TR-9 TR-11: Per-tenant, per-product RBAC metadata attached to an admin action.</summary>
/// <param name="TenantId">Tenant identifier for the administrative action.</param>
/// <param name="PrincipalId">Authenticated principal performing the action.</param>
/// <param name="ProductIds">Product identifiers the principal is authorized to administer.</param>
/// <param name="RoleIds">Role identifiers granted to the principal.</param>
public sealed record AdminRbacMetadata(
    string TenantId,
    string PrincipalId,
    IReadOnlyCollection<string> ProductIds,
    IReadOnlyCollection<string> RoleIds);

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-11: Product and environment scoped flag draft mutation.</summary>
/// <param name="FlagKey">Feature flag key.</param>
/// <param name="EnvironmentName">Environment containing the draft.</param>
/// <param name="ProductScope">Product identifiers allowed to evaluate the flag.</param>
/// <param name="ValueType">Feature flag value type.</param>
/// <param name="DefaultValue">Serialized default value for this lightweight runtime foundation.</param>
/// <param name="RuleDescriptions">Human-readable rule change descriptions for audit history.</param>
/// <param name="Reason">Reason for the draft mutation.</param>
/// <param name="RbacMetadata">Tenant, principal, product, and role metadata for the actor.</param>
public sealed record FeatureFlagDraftMutation(
    string FlagKey,
    string EnvironmentName,
    IReadOnlyCollection<string> ProductScope,
    string ValueType,
    string DefaultValue,
    IReadOnlyCollection<string> RuleDescriptions,
    string Reason,
    AdminRbacMetadata RbacMetadata);

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-11: Immutable snapshot of an authored flag draft.</summary>
/// <param name="FlagKey">Feature flag key.</param>
/// <param name="EnvironmentName">Environment containing the draft.</param>
/// <param name="ProductScope">Product identifiers allowed to evaluate the flag.</param>
/// <param name="ValueType">Feature flag value type.</param>
/// <param name="DefaultValue">Serialized default value for this lightweight runtime foundation.</param>
/// <param name="RuleDescriptions">Human-readable rule descriptions.</param>
/// <param name="LastReason">Most recent authoring reason.</param>
/// <param name="LastRbacMetadata">Tenant, principal, product, and role metadata from the latest mutation.</param>
/// <param name="Revision">Monotonic draft revision.</param>
/// <param name="LastModifiedAt">UTC timestamp of the latest mutation.</param>
public sealed record FeatureFlagDraft(
    string FlagKey,
    string EnvironmentName,
    IReadOnlyCollection<string> ProductScope,
    string ValueType,
    string DefaultValue,
    IReadOnlyCollection<string> RuleDescriptions,
    string LastReason,
    AdminRbacMetadata LastRbacMetadata,
    long Revision,
    DateTimeOffset LastModifiedAt);

/// <summary>FR-9 FR-11 TR-9 TR-11: Request to publish a flag draft for one environment.</summary>
/// <param name="FlagKey">Feature flag key.</param>
/// <param name="EnvironmentName">Environment to publish.</param>
/// <param name="Reason">Reason for the publish action.</param>
/// <param name="RbacMetadata">Tenant, principal, product, and role metadata for the actor.</param>
public sealed record FeatureFlagPublishAction(
    string FlagKey,
    string EnvironmentName,
    string Reason,
    AdminRbacMetadata RbacMetadata);

/// <summary>FR-9 FR-11 TR-9 TR-11: Request to promote a flag draft between environments.</summary>
/// <param name="FlagKey">Feature flag key.</param>
/// <param name="SourceEnvironmentName">Source environment for promotion.</param>
/// <param name="TargetEnvironmentName">Target environment for promotion.</param>
/// <param name="Reason">Reason for the promotion action.</param>
/// <param name="RbacMetadata">Tenant, principal, product, and role metadata for the actor.</param>
public sealed record FeatureFlagPromotionAction(
    string FlagKey,
    string SourceEnvironmentName,
    string TargetEnvironmentName,
    string Reason,
    AdminRbacMetadata RbacMetadata);

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-10 TR-11: Immutable admin audit entry.</summary>
/// <param name="Sequence">Monotonic audit sequence number.</param>
/// <param name="Action">Audit action type.</param>
/// <param name="FlagKey">Feature flag key.</param>
/// <param name="EnvironmentName">Primary environment for the action.</param>
/// <param name="TargetEnvironmentName">Target environment for promotions, otherwise null.</param>
/// <param name="ProductScope">Product identifiers allowed to evaluate the flag at the time of the action.</param>
/// <param name="ValueType">Feature flag value type at the time of the action.</param>
/// <param name="DefaultValue">Serialized default value at the time of the action.</param>
/// <param name="RuleDescriptions">Human-readable rule descriptions at the time of the action.</param>
/// <param name="Reason">Reason supplied by the actor.</param>
/// <param name="RbacMetadata">Tenant, principal, product, and role metadata for the actor.</param>
/// <param name="Revision">Draft revision at the time of the action.</param>
/// <param name="OccurredAt">UTC timestamp when the audit entry was recorded.</param>
public sealed record AdminAuditEntry(
    long Sequence,
    AdminAuditAction Action,
    string FlagKey,
    string EnvironmentName,
    string? TargetEnvironmentName,
    IReadOnlyCollection<string> ProductScope,
    string ValueType,
    string DefaultValue,
    IReadOnlyCollection<string> RuleDescriptions,
    string Reason,
    AdminRbacMetadata RbacMetadata,
    long Revision,
    DateTimeOffset OccurredAt);

/// <summary>TR-10 TR-11: Lightweight admin runtime metric snapshot.</summary>
/// <param name="DraftCount">Current number of in-memory flag drafts.</param>
/// <param name="AuditEntryCount">Current number of append-only audit entries.</param>
/// <param name="PublishCount">Number of publish audit entries.</param>
/// <param name="PromotionCount">Number of promotion audit entries.</param>
public sealed record AdminRuntimeMetrics(
    int DraftCount,
    int AuditEntryCount,
    int PublishCount,
    int PromotionCount);
