using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SharpNinja.FeatureFlags.Admin.Data.SqlServer;

/// <summary>FR-9 FR-11 TR-9: SQL Server-specific <see cref="AdminDbContext"/> that configures the SQL Server provider.</summary>
public sealed class SqlServerAdminDbContext : AdminDbContext
{
    /// <summary>Initializes a new instance of <see cref="SqlServerAdminDbContext"/> with caller-supplied options.</summary>
    /// <param name="options">DbContext configuration options.</param>
    public SqlServerAdminDbContext(DbContextOptions<SqlServerAdminDbContext> options)
        : base(options)
    {
    }
}

/// <summary>FR-9 FR-11: Design-time factory enabling <c>dotnet ef migrations</c> tooling for the SQL Server provider.</summary>
internal sealed class SqlServerAdminDbContextFactory : IDesignTimeDbContextFactory<SqlServerAdminDbContext>
{
    /// <inheritdoc />
    public SqlServerAdminDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<SqlServerAdminDbContext> options = new DbContextOptionsBuilder<SqlServerAdminDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=featureflags_design;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsAssembly("SharpNinja.FeatureFlags.Admin.Data.SqlServer"))
            .Options;

        return new SqlServerAdminDbContext(options);
    }
}
