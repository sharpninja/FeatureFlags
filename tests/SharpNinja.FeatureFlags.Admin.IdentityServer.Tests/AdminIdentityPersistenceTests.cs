using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Admin.IdentityServer;
using Xunit;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer.Tests;

/// <summary>TEST-GATEWAY-006: persisted Admin identity must survive a fresh provider.</summary>
public sealed class AdminIdentityPersistenceTests
{
    /// <summary>Tenant, product, and role grants remain after the original provider is disposed.</summary>
    [Fact]
    public async Task UserAndGrantsSurviveFreshProvider()
    {
        string databasePath = Path.Combine(
            Path.GetTempPath(), "featureflags-identity-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            await using (ServiceProvider first = BuildProvider(databasePath))
            {
                await using AdminIdentityDbContext context =
                    first.GetRequiredService<AdminIdentityDbContext>();
                await context.Database.EnsureCreatedAsync();
                context.Users.Add(new SharpNinjaAdminUser
                {
                    Id = "admin-persist-1",
                    UserName = "operator",
                    NormalizedUserName = "OPERATOR",
                    TenantId = "tenant-a",
                    Products = "truckmate,gigdriving",
                    Roles = "Publisher",
                    DisplayName = "Operator",
                });
                await context.SaveChangesAsync();
            }

            await using (ServiceProvider second = BuildProvider(databasePath))
            {
                await using AdminIdentityDbContext context =
                    second.GetRequiredService<AdminIdentityDbContext>();
                SharpNinjaAdminUser user = await context.Users.SingleAsync(
                    item => item.Id == "admin-persist-1");
                Assert.Equal("tenant-a", user.TenantId);
                Assert.Equal("truckmate,gigdriving", user.Products);
                Assert.Equal("Publisher", user.Roles);
                Assert.Equal("Operator", user.DisplayName);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static ServiceProvider BuildProvider(string databasePath) =>
        new ServiceCollection()
            .AddDbContext<AdminIdentityDbContext>(
                options => options.UseSqlite("Data Source=" + databasePath))
            .BuildServiceProvider();
}
