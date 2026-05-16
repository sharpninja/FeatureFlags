using System.Security.Claims;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer;

/// <summary>TR-9 TR-11: canonical seed data for the SharpNinja Admin IdentityServer host (clients, scopes, identity resources).</summary>
/// <remarks>
/// Stateless. Idempotent: existing users, clients, and resources are detected by key and not duplicated.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public static class SeedData
{
    /// <summary>TR-9: canonical Admin client id used by the Admin.Blazor host.</summary>
    public const string AdminClientId = "sharpninja-admin";

    /// <summary>TR-9: canonical API scope name shared by Admin and Distribution.</summary>
    public const string AdminApiScope = "sharpninja.admin.api";

    /// <summary>TR-9: canonical identity resource exposing the SharpNinja RBAC claim types.</summary>
    public const string RbacIdentityResource = "sharpninja_rbac";

    /// <summary>TR-9: client_credentials seed client used by integration tests and service-to-service flows.</summary>
    public const string ServiceClientId = "sharpninja-admin-service";

    /// <summary>TR-9: applies seed clients, scopes, and identity resources to the supplied options.</summary>
    /// <param name="options">Options instance to populate.</param>
    /// <param name="adminClientRedirectUris">Allowed redirect URIs for the Admin.Blazor host (e.g. <c>http://admin-blazor:8080/signin-oidc</c>).</param>
    /// <param name="adminClientPostLogoutRedirectUris">Allowed post-logout redirect URIs for the Admin.Blazor host.</param>
    /// <param name="serviceClientSecret">Shared secret for the client_credentials seed client.</param>
    public static void ApplyDefaults(
        AdminIdentityServerOptions options,
        IEnumerable<string> adminClientRedirectUris,
        IEnumerable<string> adminClientPostLogoutRedirectUris,
        string serviceClientSecret)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(adminClientRedirectUris);
        ArgumentNullException.ThrowIfNull(adminClientPostLogoutRedirectUris);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceClientSecret);

        var openId = new AdminIdentityResourceOptions("openid", "OpenID");
        openId.UserClaims.Add(ClaimTypes.NameIdentifier);
        openId.UserClaims.Add("sub");
        options.IdentityResources.Add(openId);

        var profile = new AdminIdentityResourceOptions("profile", "Profile");
        profile.UserClaims.Add(ClaimTypes.Name);
        profile.UserClaims.Add("name");
        options.IdentityResources.Add(profile);

        var rbac = new AdminIdentityResourceOptions(RbacIdentityResource, "SharpNinja Admin RBAC");
        rbac.UserClaims.Add(SharpNinjaAdminClaimDefaults.TenantClaimType);
        rbac.UserClaims.Add(SharpNinjaAdminClaimDefaults.ProductsClaimType);
        rbac.UserClaims.Add(ClaimTypes.Role);
        options.IdentityResources.Add(rbac);

        var apiScope = new AdminIdentityApiScopeOptions(AdminApiScope, "SharpNinja Admin API");
        apiScope.UserClaims.Add(SharpNinjaAdminClaimDefaults.TenantClaimType);
        apiScope.UserClaims.Add(SharpNinjaAdminClaimDefaults.ProductsClaimType);
        apiScope.UserClaims.Add(ClaimTypes.Role);
        apiScope.UserClaims.Add(ClaimTypes.Name);
        apiScope.UserClaims.Add(ClaimTypes.NameIdentifier);
        options.ApiScopes.Add(apiScope);

        var adminClient = new AdminIdentityClientOptions(AdminClientId, "SharpNinja Admin (Blazor)")
        {
            AllowAuthorizationCodeFlow = true,
            RequirePkce = true,
            RequireClientSecret = false,
            AllowOfflineAccess = true,
        };
        adminClient.AllowedScopes.Add("openid");
        adminClient.AllowedScopes.Add("profile");
        adminClient.AllowedScopes.Add(RbacIdentityResource);
        adminClient.AllowedScopes.Add(AdminApiScope);
        foreach (string uri in adminClientRedirectUris)
        {
            adminClient.RedirectUris.Add(uri);
        }

        foreach (string uri in adminClientPostLogoutRedirectUris)
        {
            adminClient.PostLogoutRedirectUris.Add(uri);
        }

        options.Clients.Add(adminClient);

        var serviceClient = new AdminIdentityClientOptions(ServiceClientId, "SharpNinja Admin (Service)")
        {
            AllowAuthorizationCodeFlow = false,
            AllowClientCredentialsFlow = true,
            RequirePkce = false,
            RequireClientSecret = true,
        };
        serviceClient.AllowedScopes.Add(AdminApiScope);
        serviceClient.ClientSecrets.Add(serviceClientSecret);
        options.Clients.Add(serviceClient);
    }
}
