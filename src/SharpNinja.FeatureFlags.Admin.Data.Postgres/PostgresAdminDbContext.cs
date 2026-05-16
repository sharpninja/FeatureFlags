using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SharpNinja.FeatureFlags.Admin.Data.Postgres;

/// <summary>FR-9 FR-11 TR-9: PostgreSQL-specific <see cref="AdminDbContext"/> that configures the Npgsql provider.</summary>
public sealed class PostgresAdminDbContext : AdminDbContext
{
    /// <summary>Initializes a new instance of <see cref="PostgresAdminDbContext"/> with caller-supplied options.</summary>
    /// <param name="options">DbContext configuration options.</param>
    public PostgresAdminDbContext(DbContextOptions<PostgresAdminDbContext> options)
        : base(options)
    {
    }
}

/// <summary>FR-9 FR-11: Design-time factory enabling <c>dotnet ef migrations</c> tooling for the PostgreSQL provider.</summary>
internal sealed class PostgresAdminDbContextFactory : IDesignTimeDbContextFactory<PostgresAdminDbContext>
{
    /// <inheritdoc />
    public PostgresAdminDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<PostgresAdminDbContext> options = new DbContextOptionsBuilder<PostgresAdminDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=featureflags_design",
                npgsql => npgsql.MigrationsAssembly("SharpNinja.FeatureFlags.Admin.Data.Postgres"))
            .Options;

        return new PostgresAdminDbContext(options);
    }
}
