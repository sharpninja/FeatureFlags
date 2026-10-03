using Xunit;
using Microsoft.EntityFrameworkCore;
using SharpNinja.FeatureFlags.Admin.Data.Postgres;

namespace SharpNinja.FeatureFlags.Admin.Data.Tests;

/// <summary>Production PostgreSQL Admin migrations must be visible to EF before Octopus can report Ready.</summary>
public sealed class PostgresAdminMigrationDiscoveryTests
{
    /// <summary>Both draft and audit migrations are discoverable without contacting PostgreSQL.</summary>
    [Fact]
    public void DraftAndAuditMigrationsAreDiscoveredByRuntimeContext()
    {
        var options = new DbContextOptionsBuilder<PostgresAdminDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=featureflags_migration_discovery",
                npgsql => npgsql.MigrationsAssembly(
                    "SharpNinja.FeatureFlags.Admin.Data.Postgres"))
            .Options;

        using var context = new PostgresAdminDbContext(options);
        string[] migrations = context.Database.GetMigrations().ToArray();

        Assert.Contains("20250516000000_InitialCreate", migrations);
        Assert.Contains("20250516000001_AddAuditEntries", migrations);
    }
}
