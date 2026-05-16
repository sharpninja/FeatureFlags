using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer;

/// <summary>TR-9 TR-11: DI registration helpers for the embedded SharpNinja Admin IdentityServer host.</summary>
/// <remarks>
/// Registration is idempotent for the IdentityServer services it owns; consumer registrations are preserved.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public static class AdminIdentityServerServiceCollectionExtensions
{
    /// <summary>Registers Duende IdentityServer with ASP.NET Identity, the SharpNinja profile service, and seeded clients/scopes/resources.</summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">Callback that populates <see cref="AdminIdentityServerOptions"/>.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSharpNinjaAdminIdentityServer(
        this IServiceCollection services,
        Action<AdminIdentityServerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var configurationOptions = new AdminIdentityServerOptions("http://localhost");
        configure(configurationOptions);

        services.AddSingleton<IOptions<AdminIdentityServerOptions>>(Options.Create(configurationOptions));

        services
            .AddIdentity<SharpNinjaAdminUser, IdentityRole>(identityOptions =>
            {
                identityOptions.Password.RequireDigit = false;
                identityOptions.Password.RequireLowercase = false;
                identityOptions.Password.RequireNonAlphanumeric = false;
                identityOptions.Password.RequireUppercase = false;
                identityOptions.Password.RequiredLength = 8;
                identityOptions.User.RequireUniqueEmail = false;
            })
            .AddEntityFrameworkStores<AdminIdentityDbContext>()
            .AddDefaultTokenProviders();

        var identityServerBuilder = services.AddIdentityServer(serverOptions =>
        {
            serverOptions.IssuerUri = configurationOptions.Issuer;
            serverOptions.EmitStaticAudienceClaim = true;
            if (!string.IsNullOrWhiteSpace(configurationOptions.LicenseKey))
            {
                serverOptions.LicenseKey = configurationOptions.LicenseKey;
            }
        });

        identityServerBuilder.AddInMemoryClients(BuildClients(configurationOptions));
        identityServerBuilder.AddInMemoryApiScopes(BuildApiScopes(configurationOptions));
        identityServerBuilder.AddInMemoryIdentityResources(BuildIdentityResources(configurationOptions));

        X509Certificate2? signingCertificate = ResolveSigningCertificate(configurationOptions.SigningCertificate);
        if (signingCertificate is not null)
        {
            identityServerBuilder.AddSigningCredential(signingCertificate);
        }
        else
        {
            identityServerBuilder.AddDeveloperSigningCredential(persistKey: false);
        }

        identityServerBuilder.AddAspNetIdentity<SharpNinjaAdminUser>();
        identityServerBuilder.AddProfileService<SharpNinjaAdminProfileService>();

        return services;
    }

    private static IEnumerable<Client> BuildClients(AdminIdentityServerOptions options)
    {
        foreach (AdminIdentityClientOptions clientOptions in options.Clients)
        {
            var grantTypes = new List<string>();
            if (clientOptions.AllowAuthorizationCodeFlow)
            {
                grantTypes.Add(GrantType.AuthorizationCode);
            }

            if (clientOptions.AllowClientCredentialsFlow)
            {
                grantTypes.Add(GrantType.ClientCredentials);
            }

            var client = new Client
            {
                ClientId = clientOptions.ClientId,
                ClientName = clientOptions.ClientName,
                AllowedGrantTypes = grantTypes,
                RequirePkce = clientOptions.RequirePkce,
                RequireClientSecret = clientOptions.RequireClientSecret,
                AllowOfflineAccess = clientOptions.AllowOfflineAccess,
                AllowedScopes = clientOptions.AllowedScopes.ToList(),
                RedirectUris = clientOptions.RedirectUris.ToList(),
                PostLogoutRedirectUris = clientOptions.PostLogoutRedirectUris.ToList(),
                ClientSecrets = clientOptions.ClientSecrets
                    .Select(secret => new Secret(HashSecret(secret)))
                    .ToList(),
            };

            yield return client;
        }
    }

    private static IEnumerable<ApiScope> BuildApiScopes(AdminIdentityServerOptions options)
    {
        foreach (AdminIdentityApiScopeOptions scope in options.ApiScopes)
        {
            var apiScope = new ApiScope(scope.Name, scope.DisplayName);
            foreach (string claim in scope.UserClaims)
            {
                apiScope.UserClaims.Add(claim);
            }

            yield return apiScope;
        }
    }

    private static IEnumerable<IdentityResource> BuildIdentityResources(AdminIdentityServerOptions options)
    {
        foreach (AdminIdentityResourceOptions resource in options.IdentityResources)
        {
            IdentityResource identityResource = resource.Name switch
            {
                "openid" => new IdentityResources.OpenId(),
                "profile" => new IdentityResources.Profile(),
                _ => new IdentityResource(resource.Name, resource.DisplayName, resource.UserClaims),
            };

            foreach (string claim in resource.UserClaims)
            {
                if (!identityResource.UserClaims.Contains(claim))
                {
                    identityResource.UserClaims.Add(claim);
                }
            }

            yield return identityResource;
        }
    }

    private static string HashSecret(string secret)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(secret);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private static X509Certificate2? ResolveSigningCertificate(AdminIdentityServerSigningCertificateOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Path))
        {
#pragma warning disable SYSLIB0057
            return new X509Certificate2(
                options.Path,
                options.Password,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
#pragma warning restore SYSLIB0057
        }

        if (!string.IsNullOrWhiteSpace(options.Thumbprint)
            && options.StoreName is not null
            && options.StoreLocation is not null)
        {
            using var store = new X509Store(options.StoreName.Value, options.StoreLocation.Value);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection found = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                options.Thumbprint,
                validOnly: false);
            return found.Count > 0 ? found[0] : null;
        }

        return null;
    }
}
