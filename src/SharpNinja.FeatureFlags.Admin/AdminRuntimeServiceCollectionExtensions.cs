using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace SharpNinja.FeatureFlags.Admin;

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-10 TR-11: DI registration extensions for the Admin runtime.</summary>
/// <remarks>
/// Registration is idempotent for the admin-runtime services it owns; consumer registrations are preserved.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-11"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public static class AdminRuntimeServiceCollectionExtensions
{
    /// <summary>Registers the Admin runtime foundation, auth hooks, RBAC, store abstraction, and immutable audit service.</summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">Optional Admin runtime configuration callback.</param>
    /// <param name="configureAuthentication">Optional production authentication hook, typically used to add OIDC.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSharpNinjaFeatureFlagsAdminRuntime(
        this IServiceCollection services,
        Action<AdminRuntimeOptions>? configure = null,
        Action<AuthenticationBuilder>? configureAuthentication = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new AdminRuntimeOptions();
        configure?.Invoke(options);
        options.Validate();

        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IOptions<AdminRuntimeOptions>>(Options.Create(options));
        services.AddSingleton<IAdminRuntimeStore, InMemoryAdminRuntimeStore>();
        services.AddSingleton<IAdminRbacAuthorizer, DefaultAdminRbacAuthorizer>();
        services.AddSingleton<IAdminActorResolver, ClaimsAdminActorResolver>();
        services.AddSingleton<IAdminRuntimeService, InMemoryAdminRuntimeService>();

        AuthenticationBuilder authenticationBuilder = services.AddAuthentication(authenticationOptions =>
        {
            authenticationOptions.DefaultAuthenticateScheme = options.Authentication.AuthenticationScheme;
            authenticationOptions.DefaultChallengeScheme = options.Authentication.ChallengeScheme ?? options.Authentication.AuthenticationScheme;
            authenticationOptions.DefaultForbidScheme = options.Authentication.ForbidScheme ?? options.Authentication.AuthenticationScheme;
        });

        if (options.Authentication.Mode == AdminAuthenticationMode.Test)
        {
            authenticationBuilder.AddScheme<AdminTestAuthenticationOptions, AdminTestAuthenticationHandler>(
                options.Authentication.AuthenticationScheme,
                static _ => { });
        }

        configureAuthentication?.Invoke(authenticationBuilder);

        services.AddAuthorization(authorizationOptions =>
        {
            AddPolicy(authorizationOptions, AdminPolicyNames.Read, options.Authentication.Claims, AdminRoleNames.Viewer, AdminRoleNames.Editor, AdminRoleNames.Publisher, AdminRoleNames.Promoter, AdminRoleNames.KeyAdmin);
            AddPolicy(authorizationOptions, AdminPolicyNames.Edit, options.Authentication.Claims, AdminRoleNames.Editor, AdminRoleNames.KeyAdmin);
            AddPolicy(authorizationOptions, AdminPolicyNames.Publish, options.Authentication.Claims, AdminRoleNames.Publisher, AdminRoleNames.KeyAdmin);
            AddPolicy(authorizationOptions, AdminPolicyNames.Promote, options.Authentication.Claims, AdminRoleNames.Promoter, AdminRoleNames.Publisher, AdminRoleNames.KeyAdmin);
            AddPolicy(authorizationOptions, AdminPolicyNames.KeyAdmin, options.Authentication.Claims, AdminRoleNames.KeyAdmin);
        });

        return services;
    }

    private static void AddPolicy(
        AuthorizationOptions options,
        string policyName,
        AdminClaimsMappingOptions claims,
        params string[] allowedRoles)
    {
        options.AddPolicy(
            policyName,
            policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => HasAnyRole(context.User, claims.RolesClaimType, allowedRoles)));
    }

    private static bool HasAnyRole(ClaimsPrincipal principal, string roleClaimType, IReadOnlyCollection<string> allowedRoles)
    {
        foreach (Claim claim in principal.Claims)
        {
            if (!string.Equals(claim.Type, roleClaimType, StringComparison.Ordinal)
                && !string.Equals(claim.Type, ClaimTypes.Role, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string role in claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (allowedRoles.Any(allowed => string.Equals(allowed, role, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
