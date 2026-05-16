using System.Security.Claims;

namespace SharpNinja.FeatureFlags.Admin;

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-10 TR-11: default names for Admin authentication, claims, and policies.</summary>
/// <remarks>
/// Static helper; members are stateless and safe for concurrent use.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-11"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public static class SharpNinjaAdminDefaults
{
    /// <summary>TR-9: deterministic test-authentication scheme used by non-production Admin validation.</summary>
    public const string TestAuthenticationScheme = "SharpNinjaAdminTest";

    /// <summary>TR-9: external OIDC scheme name expected once the OIDC package is wired by the coordinator.</summary>
    public const string OidcAuthenticationScheme = "SharpNinjaAdminOidc";

    /// <summary>TR-9: claim type used for tenant identity.</summary>
    public const string TenantClaimType = "sharpninja:tenant";

    /// <summary>TR-9: claim type used for product grants.</summary>
    public const string ProductsClaimType = "sharpninja:products";

    /// <summary>TR-9: claim type used for role grants when a provider does not emit <see cref="ClaimTypes.Role" />.</summary>
    public const string RolesClaimType = ClaimTypes.Role;

    /// <summary>TR-9: test-auth header carrying the deterministic principal id.</summary>
    public const string TestPrincipalHeaderName = "X-Admin-Principal";

    /// <summary>TR-9: test-auth header carrying the deterministic tenant id.</summary>
    public const string TestTenantHeaderName = "X-Admin-Tenant";

    /// <summary>TR-9: test-auth header carrying comma-delimited product grants.</summary>
    public const string TestProductsHeaderName = "X-Admin-Products";

    /// <summary>TR-9: test-auth header carrying comma-delimited Admin roles.</summary>
    public const string TestRolesHeaderName = "X-Admin-Roles";

    /// <summary>TR-9: test-auth header carrying the deterministic display name.</summary>
    public const string TestDisplayNameHeaderName = "X-Admin-Name";
}
