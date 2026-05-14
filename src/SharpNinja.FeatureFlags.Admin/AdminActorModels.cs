using System.Collections.ObjectModel;
using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace SharpNinja.FeatureFlags.Admin;

/// <summary>FR-9 FR-10 FR-11 TR-9: Admin operation rights enforced by v1 RBAC.</summary>
public enum AdminRight
{
    /// <summary>TR-9: permission to read Admin drafts, audit entries, UI, and metrics.</summary>
    Read,

    /// <summary>FR-9 TR-9: permission to create or edit Admin drafts.</summary>
    Edit,

    /// <summary>FR-9 FR-11 TR-9: permission to publish an Admin draft.</summary>
    Publish,

    /// <summary>FR-9 FR-11 TR-9: permission to promote an Admin draft between environments.</summary>
    Promote,

    /// <summary>TR-9: permission to administer product API keys and signing material.</summary>
    KeyAdmin,
}

/// <summary>TR-9 TR-11: authenticated Admin actor resolved from claims.</summary>
/// <param name="PrincipalId">Stable principal identifier.</param>
/// <param name="DisplayName">Human-readable display name.</param>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="ProductIds">Product grants assigned to the actor.</param>
/// <param name="RoleIds">Admin roles assigned to the actor.</param>
public sealed record AdminActor(
    string PrincipalId,
    string DisplayName,
    string TenantId,
    IReadOnlyCollection<string> ProductIds,
    IReadOnlyCollection<string> RoleIds)
{
    /// <summary>TR-9: converts the actor to auditable RBAC metadata for runtime mutations.</summary>
    /// <returns>RBAC metadata for audit entries.</returns>
    public AdminRbacMetadata ToRbacMetadata() =>
        new(TenantId, PrincipalId, ProductIds, RoleIds);
}

/// <summary>FR-9 FR-10 FR-11 TR-9: tenant, product, and environment scope for an Admin authorization decision.</summary>
/// <param name="TenantId">Tenant identifier being accessed.</param>
/// <param name="ProductIds">Product identifiers being accessed.</param>
/// <param name="EnvironmentName">Optional environment being accessed.</param>
public sealed record AdminResourceScope(
    string TenantId,
    IReadOnlyCollection<string> ProductIds,
    string? EnvironmentName);

/// <summary>TR-9 TR-11: resolves authenticated Admin actors from ASP.NET Core claims principals.</summary>
public interface IAdminActorResolver
{
    /// <summary>TR-9: resolves the Admin actor from an authenticated principal.</summary>
    /// <param name="principal">Claims principal from the active authentication handler.</param>
    /// <returns>The resolved Admin actor.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when required claims are missing.</exception>
    AdminActor Resolve(ClaimsPrincipal principal);
}

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-11: product and tenant aware Admin RBAC decision service.</summary>
public interface IAdminRbacAuthorizer
{
    /// <summary>TR-9: authorizes one Admin permission against a scoped resource.</summary>
    /// <param name="actor">Authenticated Admin actor.</param>
    /// <param name="right">Requested Admin right.</param>
    /// <param name="scope">Tenant/product/environment scope being accessed.</param>
    /// <returns>Authorization result.</returns>
    AdminAuthorizationResult Authorize(AdminActor actor, AdminRight right, AdminResourceScope scope);

    /// <summary>TR-9: authorizes one Admin permission from persisted RBAC metadata.</summary>
    /// <param name="metadata">RBAC metadata attached to a runtime request.</param>
    /// <param name="right">Requested Admin right.</param>
    /// <param name="scope">Tenant/product/environment scope being accessed.</param>
    /// <returns>Authorization result.</returns>
    AdminAuthorizationResult Authorize(AdminRbacMetadata metadata, AdminRight right, AdminResourceScope scope);
}

/// <summary>TR-9 TR-11: result of an Admin RBAC decision.</summary>
/// <param name="Succeeded">Whether authorization succeeded.</param>
/// <param name="FailureReason">Failure reason when authorization failed.</param>
public sealed record AdminAuthorizationResult(bool Succeeded, string FailureReason)
{
    /// <summary>TR-9: successful Admin authorization result.</summary>
    public static AdminAuthorizationResult Success { get; } = new(true, "");

    /// <summary>TR-9: creates a failed Admin authorization result.</summary>
    /// <param name="reason">Failure reason.</param>
    /// <returns>Failed authorization result.</returns>
    public static AdminAuthorizationResult Deny(string reason) => new(false, reason);
}

internal sealed class ClaimsAdminActorResolver : IAdminActorResolver
{
    private readonly AdminClaimsMappingOptions claims;

    public ClaimsAdminActorResolver(IOptions<AdminRuntimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        claims = options.Value.Authentication.Claims;
    }

    public AdminActor Resolve(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("Admin principal is not authenticated.");
        }

        string principalId = ReadRequiredClaim(principal, claims.PrincipalIdClaimType, "principal id");
        string displayName = ReadOptionalClaim(principal, claims.DisplayNameClaimType) ?? principalId;
        string tenantId = ReadRequiredClaim(principal, claims.TenantIdClaimType, "tenant id");
        string[] productIds = ReadDelimitedClaims(principal, claims.ProductsClaimType);
        string[] roleIds = ReadDelimitedClaims(principal, claims.RolesClaimType);

        if (productIds.Length == 0)
        {
            throw new UnauthorizedAccessException("Admin principal has no product grants.");
        }

        if (roleIds.Length == 0)
        {
            throw new UnauthorizedAccessException("Admin principal has no role grants.");
        }

        return new AdminActor(
            principalId,
            displayName,
            tenantId,
            ToReadOnlyCollection(productIds),
            ToReadOnlyCollection(roleIds));
    }

    private static string ReadRequiredClaim(ClaimsPrincipal principal, string claimType, string displayName)
    {
        string? value = ReadOptionalClaim(principal, claimType);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UnauthorizedAccessException($"Admin principal is missing a required {displayName} claim.");
        }

        return value.Trim();
    }

    private static string? ReadOptionalClaim(ClaimsPrincipal principal, string claimType) =>
        principal.Claims.FirstOrDefault(claim => string.Equals(claim.Type, claimType, StringComparison.Ordinal))?.Value;

    private static string[] ReadDelimitedClaims(ClaimsPrincipal principal, string claimType)
    {
        List<string> values = [];
        foreach (Claim claim in principal.Claims)
        {
            if (!string.Equals(claim.Type, claimType, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string part in claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!ContainsOrdinal(values, part))
                {
                    values.Add(part);
                }
            }
        }

        return values.ToArray();
    }

    private static ReadOnlyCollection<string> ToReadOnlyCollection(IEnumerable<string> values) =>
        Array.AsReadOnly(values.ToArray());

    private static bool ContainsOrdinal(IEnumerable<string> values, string value) =>
        values.Any(candidate => string.Equals(candidate, value, StringComparison.Ordinal));
}

internal sealed class DefaultAdminRbacAuthorizer : IAdminRbacAuthorizer
{
    public AdminAuthorizationResult Authorize(AdminActor actor, AdminRight right, AdminResourceScope scope)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return Authorize(actor.ToRbacMetadata(), right, scope);
    }

    public AdminAuthorizationResult Authorize(AdminRbacMetadata metadata, AdminRight right, AdminResourceScope scope)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(scope);

        if (!TenantMatches(metadata.TenantId, scope.TenantId))
        {
            return AdminAuthorizationResult.Deny("Admin actor is not authorized for the requested tenant.");
        }

        foreach (string productId in scope.ProductIds)
        {
            if (!ContainsGrant(metadata.ProductIds, productId))
            {
                return AdminAuthorizationResult.Deny($"Admin actor is not authorized for product '{productId}'.");
            }
        }

        if (!HasRequiredRole(metadata.RoleIds, right, scope.EnvironmentName))
        {
            return AdminAuthorizationResult.Deny($"Admin actor lacks the {right} role grant.");
        }

        return AdminAuthorizationResult.Success;
    }

    private static bool TenantMatches(string actorTenantId, string resourceTenantId) =>
        string.Equals(actorTenantId, "*", StringComparison.Ordinal)
        || string.Equals(actorTenantId, resourceTenantId, StringComparison.Ordinal);

    private static bool ContainsGrant(IEnumerable<string> grants, string value) =>
        grants.Any(grant => string.Equals(grant, "*", StringComparison.Ordinal)
            || string.Equals(grant, value, StringComparison.Ordinal));

    private static bool HasRequiredRole(
        IReadOnlyCollection<string> roleIds,
        AdminRight right,
        string? environmentName)
    {
        if (ContainsGrant(roleIds, AdminRoleNames.KeyAdmin))
        {
            return true;
        }

        return right switch
        {
            AdminRight.Read => HasAny(roleIds, AdminRoleNames.Viewer, AdminRoleNames.Editor, AdminRoleNames.Publisher, AdminRoleNames.Promoter),
            AdminRight.Edit => HasAny(roleIds, AdminRoleNames.Editor),
            AdminRight.Publish => CanPublish(roleIds, environmentName),
            AdminRight.Promote => HasAny(roleIds, AdminRoleNames.Promoter, AdminRoleNames.Publisher),
            AdminRight.KeyAdmin => HasAny(roleIds, AdminRoleNames.KeyAdmin),
            _ => false,
        };
    }

    private static bool CanPublish(IReadOnlyCollection<string> roleIds, string? environmentName)
    {
        if (!HasAny(roleIds, AdminRoleNames.Publisher))
        {
            return false;
        }

        return !string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase)
            || HasAny(roleIds, AdminRoleNames.Editor);
    }

    private static bool HasAny(IReadOnlyCollection<string> roleIds, params string[] allowedRoles) =>
        allowedRoles.Any(allowed => roleIds.Any(role => string.Equals(role, allowed, StringComparison.Ordinal)));
}
