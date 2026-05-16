using Microsoft.EntityFrameworkCore;
using SharpNinja.FeatureFlags.Admin.Data.Entities;

namespace SharpNinja.FeatureFlags.Admin.Data;

/// <summary>FR-9 FR-11 TR-9: Abstract EF Core DbContext for the admin-plane store. Subclassed by each provider package.</summary>
/// <remarks>
/// EF Core <see cref="Microsoft.EntityFrameworkCore.DbContext"/> derivative; not thread-safe.
/// Register with <c>AddDbContextPool</c> only when the host can guarantee scoped reset semantics.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-11"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// </remarks>
public abstract class AdminDbContext : DbContext
{
    /// <summary>Initializes a new instance of <see cref="AdminDbContext"/> with caller-supplied options.</summary>
    /// <param name="options">DbContext configuration options provided by the DI container or design-time factory.</param>
    protected AdminDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>Gets the flag draft table.</summary>
    public DbSet<FlagDraftEntity> FlagDrafts => Set<FlagDraftEntity>();

    /// <summary>Gets the append-only audit-entry table.</summary>
    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureFlagDrafts(modelBuilder);
        ConfigureAuditEntries(modelBuilder);
    }

    private static void ConfigureFlagDrafts(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FlagDraftEntity>();

        entity.ToTable("FlagDrafts");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.HasIndex(e => new { e.FlagKey, e.EnvironmentName }).IsUnique();

        entity.Property(e => e.FlagKey).HasMaxLength(256).IsRequired();
        entity.Property(e => e.EnvironmentName).HasMaxLength(128).IsRequired();
        entity.Property(e => e.ValueType).HasMaxLength(64).IsRequired();
        entity.Property(e => e.DefaultValue).HasMaxLength(4096).IsRequired();
        entity.Property(e => e.LastReason).HasMaxLength(2048).IsRequired();
        entity.Property(e => e.ProductScopeJson).HasMaxLength(8192).IsRequired();
        entity.Property(e => e.RuleDescriptionsJson).HasMaxLength(65536).IsRequired();
        entity.Property(e => e.Revision).IsRequired();
        entity.Property(e => e.LastModifiedAt).IsRequired();

        entity.OwnsOne(e => e.LastRbacMetadata, rbac =>
        {
            rbac.Property(r => r.TenantId).HasMaxLength(256).IsRequired().HasColumnName("LastRbac_TenantId");
            rbac.Property(r => r.PrincipalId).HasMaxLength(256).IsRequired().HasColumnName("LastRbac_PrincipalId");
            rbac.Property(r => r.ProductIdsJson).HasMaxLength(8192).IsRequired().HasColumnName("LastRbac_ProductIdsJson");
            rbac.Property(r => r.RoleIdsJson).HasMaxLength(8192).IsRequired().HasColumnName("LastRbac_RoleIdsJson");
        });
    }

    private static void ConfigureAuditEntries(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AuditEntryEntity>();

        entity.ToTable("AuditEntries");
        entity.HasKey(e => e.Sequence);
        entity.Property(e => e.Sequence).ValueGeneratedOnAdd();

        entity.Property(e => e.Action).IsRequired();
        entity.Property(e => e.FlagKey).HasMaxLength(256).IsRequired();
        entity.Property(e => e.EnvironmentName).HasMaxLength(128).IsRequired();
        entity.Property(e => e.TargetEnvironmentName).HasMaxLength(128);
        entity.Property(e => e.ValueType).HasMaxLength(64).IsRequired();
        entity.Property(e => e.DefaultValue).HasMaxLength(4096).IsRequired();
        entity.Property(e => e.Reason).HasMaxLength(2048).IsRequired();
        entity.Property(e => e.ProductScopeJson).HasMaxLength(8192).IsRequired();
        entity.Property(e => e.RuleDescriptionsJson).HasMaxLength(65536).IsRequired();
        entity.Property(e => e.Revision).IsRequired();
        entity.Property(e => e.OccurredAt).IsRequired();

        entity.OwnsOne(e => e.RbacMetadata, rbac =>
        {
            rbac.Property(r => r.TenantId).HasMaxLength(256).IsRequired().HasColumnName("Rbac_TenantId");
            rbac.Property(r => r.PrincipalId).HasMaxLength(256).IsRequired().HasColumnName("Rbac_PrincipalId");
            rbac.Property(r => r.ProductIdsJson).HasMaxLength(8192).IsRequired().HasColumnName("Rbac_ProductIdsJson");
            rbac.Property(r => r.RoleIdsJson).HasMaxLength(8192).IsRequired().HasColumnName("Rbac_RoleIdsJson");
        });
    }
}
