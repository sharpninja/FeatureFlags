using System.Security.Claims;

namespace SharpNinja.FeatureFlags.Admin;

/// <summary>TR-9 TR-11: Admin authentication mode selected by the host composition root.</summary>
public enum AdminAuthenticationMode
{
    /// <summary>TR-9: deterministic header-backed authentication for repeatable tests.</summary>
    Test,

    /// <summary>TR-9: externally wired OIDC authentication for production Admin deployments.</summary>
    Oidc,
}

/// <summary>TR-9 TR-11: configurable Admin authentication and claim-mapping options.</summary>
public sealed record AdminAuthenticationOptions
{
    /// <summary>TR-9: authentication mode used by the Admin host.</summary>
    public AdminAuthenticationMode Mode { get; set; } = AdminAuthenticationMode.Test;

    /// <summary>TR-9: active authentication scheme for Admin endpoints.</summary>
    public string AuthenticationScheme { get; set; } = SharpNinjaAdminDefaults.TestAuthenticationScheme;

    /// <summary>TR-9: claim mapping used by the Admin actor resolver.</summary>
    public AdminClaimsMappingOptions Claims { get; } = new();

    /// <summary>TR-9: OIDC configuration captured for the production wiring hook.</summary>
    public AdminOidcOptions Oidc { get; } = new();

    /// <summary>TR-9: validates authentication options before they are registered in DI.</summary>
    /// <returns>The validated options.</returns>
    /// <exception cref="ArgumentException">Thrown when a required configured value is missing.</exception>
    public AdminAuthenticationOptions Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(AuthenticationScheme);
        Claims.Validate();

        if (Mode == AdminAuthenticationMode.Oidc)
        {
            Oidc.Validate();
        }

        return this;
    }
}

/// <summary>TR-9 TR-11: claim names used to resolve Admin actors from test auth or OIDC principals.</summary>
public sealed record AdminClaimsMappingOptions
{
    /// <summary>TR-9: claim type used as the stable Admin principal id.</summary>
    public string PrincipalIdClaimType { get; set; } = ClaimTypes.NameIdentifier;

    /// <summary>TR-9: claim type used as the display name.</summary>
    public string DisplayNameClaimType { get; set; } = ClaimTypes.Name;

    /// <summary>TR-9: claim type used as the tenant identifier.</summary>
    public string TenantIdClaimType { get; set; } = SharpNinjaAdminDefaults.TenantClaimType;

    /// <summary>TR-9: claim type used for product grants.</summary>
    public string ProductsClaimType { get; set; } = SharpNinjaAdminDefaults.ProductsClaimType;

    /// <summary>TR-9: claim type used for role grants.</summary>
    public string RolesClaimType { get; set; } = SharpNinjaAdminDefaults.RolesClaimType;

    /// <summary>TR-9: validates configured claim type names.</summary>
    /// <returns>The validated options.</returns>
    /// <exception cref="ArgumentException">Thrown when a required claim type is missing.</exception>
    public AdminClaimsMappingOptions Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PrincipalIdClaimType);
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayNameClaimType);
        ArgumentException.ThrowIfNullOrWhiteSpace(TenantIdClaimType);
        ArgumentException.ThrowIfNullOrWhiteSpace(ProductsClaimType);
        ArgumentException.ThrowIfNullOrWhiteSpace(RolesClaimType);
        return this;
    }
}

/// <summary>TR-9 TR-11: production OIDC settings captured without forcing package wiring in this worker slice.</summary>
public sealed record AdminOidcOptions
{
    /// <summary>TR-9: OIDC authority URL.</summary>
    public string Authority { get; set; } = "";

    /// <summary>TR-9: OIDC client id.</summary>
    public string ClientId { get; set; } = "";

    /// <summary>TR-9: OIDC callback path used by the production handler.</summary>
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>TR-9: OIDC response type expected by the production handler.</summary>
    public string ResponseType { get; set; } = "code";

    /// <summary>TR-9: OIDC scopes requested by the production handler.</summary>
    public IList<string> Scopes { get; } = new List<string> { "openid", "profile", "email" };

    /// <summary>TR-9: validates OIDC options when OIDC mode is selected.</summary>
    /// <returns>The validated options.</returns>
    /// <exception cref="ArgumentException">Thrown when a required OIDC value is missing.</exception>
    public AdminOidcOptions Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(ClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(CallbackPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ResponseType);
        return this;
    }
}
