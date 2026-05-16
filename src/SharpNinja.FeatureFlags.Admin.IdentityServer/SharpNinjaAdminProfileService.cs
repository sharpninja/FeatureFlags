using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer;

/// <summary>TR-9 TR-11: <see cref="IProfileService"/> that projects <see cref="SharpNinjaAdminUser"/> rows into the SharpNinja Admin RBAC claim shape.</summary>
/// <remarks>
/// Scoped DI lifetime; not thread-safe across concurrent token requests. IdentityServer instantiates one per request.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public sealed class SharpNinjaAdminProfileService : IProfileService
{
    private readonly UserManager<SharpNinjaAdminUser> userManager;

    /// <summary>Initializes a new <see cref="SharpNinjaAdminProfileService"/>.</summary>
    /// <param name="userManager">ASP.NET Identity user manager.</param>
    public SharpNinjaAdminProfileService(UserManager<SharpNinjaAdminUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        this.userManager = userManager;
    }

    /// <inheritdoc />
    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? subjectId = context.Subject?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.Subject?.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return;
        }

        SharpNinjaAdminUser? user = await userManager.FindByIdAsync(subjectId).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        List<Claim> issued =
        [
            new(ClaimTypes.NameIdentifier, user.Id),
            new("sub", user.Id),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName ?? user.Id : user.DisplayName),
            new("name", string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName ?? user.Id : user.DisplayName),
            new(SharpNinjaAdminClaimDefaults.TenantClaimType, user.TenantId ?? ""),
        ];

        foreach (string product in Split(user.Products))
        {
            issued.Add(new Claim(SharpNinjaAdminClaimDefaults.ProductsClaimType, product));
        }

        foreach (string role in Split(user.Roles))
        {
            issued.Add(new Claim(ClaimTypes.Role, role));
        }

        context.IssuedClaims.AddRange(issued);
    }

    /// <inheritdoc />
    public async Task IsActiveAsync(IsActiveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? subjectId = context.Subject?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.Subject?.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(subjectId))
        {
            context.IsActive = false;
            return;
        }

        SharpNinjaAdminUser? user = await userManager.FindByIdAsync(subjectId).ConfigureAwait(false);
        context.IsActive = user is not null && !user.LockoutEnabled;
    }

    private static IEnumerable<string> Split(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (string token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return token;
        }
    }
}
