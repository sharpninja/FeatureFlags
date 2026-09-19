using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Admin.IdentityServer;
using Xunit;

namespace SharpNinja.FeatureFlags.Admin.Tests;

/// <summary>Verifies Admin startup with the configured in-memory identity store.</summary>
public sealed class AdminIdentityInMemoryStartupTests
{
    /// <summary>In-memory initialization does not invoke relational migration APIs.</summary>
    [Fact]
    public async Task EnsureAdminIdentityDatabaseCreatesInMemoryDatabaseWithoutRelationalMigrations()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddDbContext<AdminIdentityDbContext>(options =>
                options.UseInMemoryDatabase($"admin-startup-{Guid.NewGuid():N}"))
            .BuildServiceProvider();

        SharpNinjaAdminUser? seeded =
            await AdminIdentityServerApplicationBuilderExtensions
                .EnsureAdminIdentityDatabaseAsync(services, seedUser: null, seedPassword: null);

        Assert.Null(seeded);
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AdminIdentityDbContext db = scope.ServiceProvider.GetRequiredService<AdminIdentityDbContext>();
        Assert.True(await db.Database.CanConnectAsync());
    }
}
