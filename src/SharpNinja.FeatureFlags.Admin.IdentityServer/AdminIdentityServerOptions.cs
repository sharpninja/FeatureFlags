using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer;

/// <summary>TR-9 TR-11: configurable Admin IdentityServer host options.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public sealed record AdminIdentityServerOptions
{
    /// <summary>Initializes a new instance of <see cref="AdminIdentityServerOptions"/> with the configured issuer URI.</summary>
    /// <param name="issuer">Issuer URI emitted in discovery and tokens, e.g. <c>http://admin:8080</c>.</param>
    public AdminIdentityServerOptions(string issuer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        Issuer = issuer;
        Clients = new Collection<AdminIdentityClientOptions>();
        ApiScopes = new Collection<AdminIdentityApiScopeOptions>();
        IdentityResources = new Collection<AdminIdentityResourceOptions>();
        SigningCertificate = new AdminIdentityServerSigningCertificateOptions();
    }

    /// <summary>TR-9: issuer URI emitted by IdentityServer discovery.</summary>
    public string Issuer { get; }

    /// <summary>TR-9: optional Duende commercial license key. When null IdentityServer operates in Bootstrap (free, low-volume) mode.</summary>
    public string? LicenseKey { get; set; }

    /// <summary>TR-9: configured Admin IdentityServer clients.</summary>
    public Collection<AdminIdentityClientOptions> Clients { get; }

    /// <summary>TR-9: configured Admin IdentityServer API scopes.</summary>
    public Collection<AdminIdentityApiScopeOptions> ApiScopes { get; }

    /// <summary>TR-9: configured Admin IdentityServer identity resources.</summary>
    public Collection<AdminIdentityResourceOptions> IdentityResources { get; }

    /// <summary>TR-9: signing certificate resolution settings. When unset the host uses an ephemeral developer signing credential.</summary>
    public AdminIdentityServerSigningCertificateOptions SigningCertificate { get; }
}

/// <summary>TR-9 TR-11: resolution settings for the IdentityServer signing certificate.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public sealed record AdminIdentityServerSigningCertificateOptions
{
    /// <summary>Optional PFX path on disk.</summary>
    public string? Path { get; set; }

    /// <summary>Optional PFX password.</summary>
    public string? Password { get; set; }

    /// <summary>Optional Windows certificate store name.</summary>
    public StoreName? StoreName { get; set; }

    /// <summary>Optional Windows certificate store location.</summary>
    public StoreLocation? StoreLocation { get; set; }

    /// <summary>Optional Windows certificate thumbprint to resolve from the configured store.</summary>
    public string? Thumbprint { get; set; }
}

/// <summary>TR-9 TR-11: configured IdentityServer client metadata.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public sealed record AdminIdentityClientOptions
{
    /// <summary>Initializes a new client options instance.</summary>
    /// <param name="clientId">Stable client identifier.</param>
    /// <param name="clientName">Display name shown on consent screens.</param>
    public AdminIdentityClientOptions(string clientId, string clientName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        ClientId = clientId;
        ClientName = clientName;
        AllowedScopes = new Collection<string>();
        RedirectUris = new Collection<string>();
        PostLogoutRedirectUris = new Collection<string>();
        ClientSecrets = new Collection<string>();
    }

    /// <summary>Stable client identifier.</summary>
    public string ClientId { get; }

    /// <summary>Display name shown on consent screens.</summary>
    public string ClientName { get; }

    /// <summary>When true allows authorization-code + PKCE.</summary>
    public bool AllowAuthorizationCodeFlow { get; set; } = true;

    /// <summary>When true allows client_credentials flow (used by service-to-service clients).</summary>
    public bool AllowClientCredentialsFlow { get; set; }

    /// <summary>When true enables offline access (refresh tokens).</summary>
    public bool AllowOfflineAccess { get; set; }

    /// <summary>When true requires the client to PKCE.</summary>
    public bool RequirePkce { get; set; } = true;

    /// <summary>When true requires a client secret on the token endpoint.</summary>
    public bool RequireClientSecret { get; set; }

    /// <summary>OAuth scopes the client may request.</summary>
    public Collection<string> AllowedScopes { get; }

    /// <summary>Allowed redirect URIs.</summary>
    public Collection<string> RedirectUris { get; }

    /// <summary>Allowed post-logout redirect URIs.</summary>
    public Collection<string> PostLogoutRedirectUris { get; }

    /// <summary>Plaintext shared secrets, hashed during registration.</summary>
    public Collection<string> ClientSecrets { get; }
}

/// <summary>TR-9 TR-11: configured IdentityServer API scope.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public sealed record AdminIdentityApiScopeOptions
{
    /// <summary>Initializes a new API scope options instance.</summary>
    /// <param name="name">API scope name.</param>
    /// <param name="displayName">API scope display name.</param>
    public AdminIdentityApiScopeOptions(string name, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Name = name;
        DisplayName = displayName;
        UserClaims = new Collection<string>();
    }

    /// <summary>API scope name.</summary>
    public string Name { get; }

    /// <summary>API scope display name.</summary>
    public string DisplayName { get; }

    /// <summary>Claim types emitted into access tokens issued for this scope.</summary>
    public Collection<string> UserClaims { get; }
}

/// <summary>TR-9 TR-11: configured IdentityServer identity resource.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public sealed record AdminIdentityResourceOptions
{
    /// <summary>Initializes a new identity resource options instance.</summary>
    /// <param name="name">Identity resource name.</param>
    /// <param name="displayName">Identity resource display name.</param>
    public AdminIdentityResourceOptions(string name, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Name = name;
        DisplayName = displayName;
        UserClaims = new Collection<string>();
    }

    /// <summary>Identity resource name.</summary>
    public string Name { get; }

    /// <summary>Identity resource display name.</summary>
    public string DisplayName { get; }

    /// <summary>Claim types emitted in identity tokens / userinfo responses.</summary>
    public Collection<string> UserClaims { get; }
}
