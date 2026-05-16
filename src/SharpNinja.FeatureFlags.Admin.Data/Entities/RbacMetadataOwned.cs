using Microsoft.EntityFrameworkCore;

namespace SharpNinja.FeatureFlags.Admin.Data.Entities;

/// <summary>FR-9 TR-9: Owned EF Core entity embedding <see cref="SharpNinja.FeatureFlags.Admin.AdminRbacMetadata"/> into a host table column set.</summary>
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
