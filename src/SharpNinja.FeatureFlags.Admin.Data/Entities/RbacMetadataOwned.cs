using Microsoft.EntityFrameworkCore;

namespace SharpNinja.FeatureFlags.Admin.Data.Entities;

/// <summary>FR-9 TR-9: Owned EF Core entity embedding <see cref="SharpNinja.FeatureFlags.Admin.AdminRbacMetadata"/> into a host table column set.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// </remarks>
[Owned]
public sealed record class RbacMetadataOwned
{
    /// <summary>Gets or sets the tenant identifier for the administrative action.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets or sets the authenticated principal performing the action.</summary>
    public string PrincipalId { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON-serialized product identifier collection.</summary>
    public string ProductIdsJson { get; set; } = "[]";

    /// <summary>Gets or sets the JSON-serialized role identifier collection.</summary>
    public string RoleIdsJson { get; set; } = "[]";
}
