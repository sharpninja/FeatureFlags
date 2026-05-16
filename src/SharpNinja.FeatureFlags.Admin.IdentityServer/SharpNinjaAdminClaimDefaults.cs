using System.Security.Claims;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer;

/// <summary>TR-9 TR-11: Claim type constants shared with the SharpNinja Admin runtime. Duplicated here to keep this assembly free of runtime project references.</summary>
/// <remarks>
/// Static helper; members are stateless and safe for concurrent use.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public static class SharpNinjaAdminClaimDefaults
{
    /// <summary>Tenant identifier claim.</summary>
    public const string TenantClaimType = "sharpninja:tenant";

    /// <summary>Products grant claim.</summary>
    public const string ProductsClaimType = "sharpninja:products";

    /// <summary>Role claim type alias matching <see cref="ClaimTypes.Role"/>.</summary>
    public const string RolesClaimType = ClaimTypes.Role;
}
