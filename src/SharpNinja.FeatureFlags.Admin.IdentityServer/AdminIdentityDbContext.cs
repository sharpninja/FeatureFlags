using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer;

/// <summary>TR-9 TR-11: EF Core <see cref="IdentityDbContext{TUser}"/> backing SharpNinja Admin IdentityServer user storage.</summary>
/// <remarks>
/// EF Core <see cref="Microsoft.EntityFrameworkCore.DbContext"/> derivative; not thread-safe.
/// Hosts IdentityServer operational and configuration data alongside ASP.NET Core Identity tables.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public class AdminIdentityDbContext : IdentityDbContext<SharpNinjaAdminUser>
{
    /// <summary>Initializes a new instance of <see cref="AdminIdentityDbContext"/> with caller-supplied options.</summary>
    /// <param name="options">DbContext configuration options provided by the host or design-time factory.</param>
    public AdminIdentityDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.Entity<SharpNinjaAdminUser>(entity =>
        {
            entity.Property(u => u.TenantId).HasMaxLength(256).IsRequired();
            entity.Property(u => u.Products).HasMaxLength(2048).IsRequired();
            entity.Property(u => u.Roles).HasMaxLength(2048).IsRequired();
            entity.Property(u => u.DisplayName).HasMaxLength(256).IsRequired();
        });
    }
}
