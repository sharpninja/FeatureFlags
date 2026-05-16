using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer.SqlServer;

/// <summary>TR-9 TR-11: SQL Server-specific <see cref="AdminIdentityDbContext"/>.</summary>
/// <remarks>
/// SQL Server-flavored Identity context; inherits the schema invariants of <see cref="AdminIdentityDbContext"/>.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public sealed class SqlServerAdminIdentityDbContext : AdminIdentityDbContext
{
    /// <summary>Initializes a new instance of <see cref="SqlServerAdminIdentityDbContext"/>.</summary>
    /// <param name="options">DbContext configuration options.</param>
    public SqlServerAdminIdentityDbContext(DbContextOptions<SqlServerAdminIdentityDbContext> options)
        : base(options)
    {
    }
}

/// <summary>TR-9 TR-11: Design-time factory enabling <c>dotnet ef migrations</c> for the SQL Server provider.</summary>
internal sealed class SqlServerAdminIdentityDbContextFactory : IDesignTimeDbContextFactory<SqlServerAdminIdentityDbContext>
{
    /// <inheritdoc />
    public SqlServerAdminIdentityDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<SqlServerAdminIdentityDbContext> options = new DbContextOptionsBuilder<SqlServerAdminIdentityDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=featureflags_admin_identity_design;Integrated Security=True",
                sql => sql.MigrationsAssembly("SharpNinja.FeatureFlags.Admin.IdentityServer.SqlServer"))
            .Options;

        return new SqlServerAdminIdentityDbContext(options);
    }
}
