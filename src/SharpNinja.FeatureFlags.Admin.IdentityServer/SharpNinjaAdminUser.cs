using Microsoft.AspNetCore.Identity;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer;

/// <summary>TR-9 TR-11: ASP.NET Identity user backing the SharpNinja Admin IdentityServer, carrying tenant, products, and role grants.</summary>
/// <remarks>
/// Stateless after construction; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public sealed class SharpNinjaAdminUser : IdentityUser
{
    /// <summary>TR-9: tenant identifier projected into the <c>sharpninja:tenant</c> claim.</summary>
    public string TenantId { get; set; } = "";

    /// <summary>TR-9: comma-delimited product identifiers projected into the <c>sharpninja:products</c> claim.</summary>
    public string Products { get; set; } = "";

    /// <summary>TR-9: comma-delimited Admin role identifiers projected into the role claim.</summary>
    public string Roles { get; set; } = "";

    /// <summary>TR-9: display name projected into the <see cref="System.Security.Claims.ClaimTypes.Name"/> claim.</summary>
    public string DisplayName { get; set; } = "";
}
