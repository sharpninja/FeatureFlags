using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer;

/// <summary>TR-9 TR-11: Application pipeline helpers for the SharpNinja Admin IdentityServer host.</summary>
/// <remarks>
/// Registration is idempotent for the IdentityServer middleware it owns; call once per pipeline.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public static class AdminIdentityServerApplicationBuilderExtensions
{
    /// <summary>Applies pending <see cref="AdminIdentityDbContext"/> migrations and seeds a deterministic Admin user when present.</summary>
    /// <param name="services">Service provider scoped to application startup.</param>
    /// <param name="seedUser">Optional seed user definition.</param>
    /// <param name="seedPassword">Password assigned to the seed user.</param>
    /// <returns>The provider-resolved seed user instance, or <see langword="null"/> when no seed user was supplied.</returns>
    public static async Task<SharpNinjaAdminUser?> EnsureAdminIdentityDatabaseAsync(
        IServiceProvider services,
        SharpNinjaAdminUser? seedUser,
        string? seedPassword)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AdminIdentityDbContext db = scope.ServiceProvider.GetRequiredService<AdminIdentityDbContext>();
        string providerName = db.Database.ProviderName ?? "";
        bool hasMigrations = db.Database.GetMigrations().Any();
        bool useMigrate = db.Database.IsRelational()
            && hasMigrations
            && !providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);
        if (useMigrate)
        {
            await db.Database.MigrateAsync().ConfigureAwait(false);
        }
        else
        {
            await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
        }

        if (seedUser is null || string.IsNullOrWhiteSpace(seedPassword))
        {
            return null;
        }

        UserManager<SharpNinjaAdminUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<SharpNinjaAdminUser>>();
        SharpNinjaAdminUser? existing = await userManager.FindByNameAsync(seedUser.UserName ?? seedUser.Id).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        IdentityResult result = await userManager.CreateAsync(seedUser, seedPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to seed SharpNinja Admin user: "
                + string.Join("; ", result.Errors.Select(e => $"{e.Code}:{e.Description}")));
        }

        return seedUser;
    }

    /// <summary>Adds the IdentityServer middleware to the request pipeline in the standard order.</summary>
    /// <param name="app">Application builder.</param>
    /// <returns>The updated application builder.</returns>
    public static IApplicationBuilder UseSharpNinjaAdminIdentityServer(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseIdentityServer();
        return app;
    }
}
