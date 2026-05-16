using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer.Postgres;

/// <summary>TR-9 TR-11: PostgreSQL-specific <see cref="AdminIdentityDbContext"/>.</summary>
/// <remarks>
/// Postgres-flavored Identity context; inherits the schema invariants of <see cref="AdminIdentityDbContext"/>.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public sealed class PostgresAdminIdentityDbContext : AdminIdentityDbContext
{
    /// <summary>Initializes a new instance of <see cref="PostgresAdminIdentityDbContext"/>.</summary>
    /// <param name="options">DbContext configuration options.</param>
    public PostgresAdminIdentityDbContext(DbContextOptions<PostgresAdminIdentityDbContext> options)
        : base(options)
    {
    }
}

/// <summary>TR-9 TR-11: Design-time factory enabling <c>dotnet ef migrations</c> for the PostgreSQL provider.</summary>
internal sealed class PostgresAdminIdentityDbContextFactory : IDesignTimeDbContextFactory<PostgresAdminIdentityDbContext>
{
    /// <inheritdoc />
    public PostgresAdminIdentityDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<PostgresAdminIdentityDbContext> options = new DbContextOptionsBuilder<PostgresAdminIdentityDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=featureflags_admin_identity_design",
                npgsql => npgsql.MigrationsAssembly("SharpNinja.FeatureFlags.Admin.IdentityServer.Postgres"))
            .Options;

        return new PostgresAdminIdentityDbContext(options);
    }
}
