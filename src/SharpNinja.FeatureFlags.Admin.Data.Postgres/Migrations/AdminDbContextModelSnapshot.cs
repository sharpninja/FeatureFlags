using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace SharpNinja.FeatureFlags.Admin.Data.Postgres.Migrations;

/// <summary>FR-9 FR-11: EF Core model snapshot for the PostgreSQL admin DbContext.</summary>
[DbContext(typeof(PostgresAdminDbContext))]
internal sealed partial class AdminDbContextModelSnapshot : ModelSnapshot
{
    /// <inheritdoc />
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.5")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("SharpNinja.FeatureFlags.Admin.Data.Entities.FlagDraftEntity", b =>
        {
            b.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            b.Property<string>("DefaultValue")
                .IsRequired()
                .HasMaxLength(4096)
                .HasColumnType("character varying(4096)");

            b.Property<string>("EnvironmentName")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)");

            b.Property<string>("FlagKey")
                .IsRequired()
                .HasMaxLength(256)
                .HasColumnType("character varying(256)");

            b.Property<DateTimeOffset>("LastModifiedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("LastReason")
                .IsRequired()
                .HasMaxLength(2048)
                .HasColumnType("character varying(2048)");

            b.Property<string>("ProductScopeJson")
                .IsRequired()
                .HasMaxLength(8192)
                .HasColumnType("character varying(8192)");

            b.Property<long>("Revision")
                .HasColumnType("bigint");

            b.Property<string>("RuleDescriptionsJson")
                .IsRequired()
                .HasMaxLength(65536)
                .HasColumnType("character varying(65536)");

            b.Property<string>("ValueType")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("character varying(64)");

            b.HasKey("Id");
            b.HasIndex("FlagKey", "EnvironmentName").IsUnique();
            b.ToTable("FlagDrafts");

            b.OwnsOne("SharpNinja.FeatureFlags.Admin.Data.Entities.RbacMetadataOwned", "LastRbacMetadata", b1 =>
            {
                b1.Property<long>("FlagDraftEntityId")
                    .HasColumnType("bigint");

                b1.Property<string>("PrincipalId")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("LastRbac_PrincipalId");

                b1.Property<string>("ProductIdsJson")
                    .IsRequired()
                    .HasMaxLength(8192)
                    .HasColumnType("character varying(8192)")
                    .HasColumnName("LastRbac_ProductIdsJson");

                b1.Property<string>("RoleIdsJson")
                    .IsRequired()
                    .HasMaxLength(8192)
                    .HasColumnType("character varying(8192)")
                    .HasColumnName("LastRbac_RoleIdsJson");

                b1.Property<string>("TenantId")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("LastRbac_TenantId");

                b1.HasKey("FlagDraftEntityId");
                b1.ToTable("FlagDrafts");
                b1.WithOwner().HasForeignKey("FlagDraftEntityId");
            });

            b.Navigation("LastRbacMetadata").IsRequired();
        });

        modelBuilder.Entity("SharpNinja.FeatureFlags.Admin.Data.Entities.AuditEntryEntity", b =>
        {
            b.Property<long>("Sequence")
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            b.Property<int>("Action")
                .HasColumnType("integer");

            b.Property<string>("DefaultValue")
                .IsRequired()
                .HasMaxLength(4096)
                .HasColumnType("character varying(4096)");

            b.Property<string>("EnvironmentName")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)");

            b.Property<string>("FlagKey")
                .IsRequired()
                .HasMaxLength(256)
                .HasColumnType("character varying(256)");

            b.Property<DateTimeOffset>("OccurredAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("ProductScopeJson")
                .IsRequired()
                .HasMaxLength(8192)
                .HasColumnType("character varying(8192)");

            b.Property<string>("Reason")
                .IsRequired()
                .HasMaxLength(2048)
                .HasColumnType("character varying(2048)");

            b.Property<long>("Revision")
                .HasColumnType("bigint");

            b.Property<string>("RuleDescriptionsJson")
                .IsRequired()
                .HasMaxLength(65536)
                .HasColumnType("character varying(65536)");

            b.Property<string?>("TargetEnvironmentName")
                .HasMaxLength(128)
                .HasColumnType("character varying(128)");

            b.Property<string>("ValueType")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("character varying(64)");

            b.HasKey("Sequence");
            b.ToTable("AuditEntries");

            b.OwnsOne("SharpNinja.FeatureFlags.Admin.Data.Entities.RbacMetadataOwned", "RbacMetadata", b1 =>
            {
                b1.Property<long>("AuditEntryEntitySequence")
                    .HasColumnType("bigint");

                b1.Property<string>("PrincipalId")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("Rbac_PrincipalId");

                b1.Property<string>("ProductIdsJson")
                    .IsRequired()
                    .HasMaxLength(8192)
                    .HasColumnType("character varying(8192)")
                    .HasColumnName("Rbac_ProductIdsJson");

                b1.Property<string>("RoleIdsJson")
                    .IsRequired()
                    .HasMaxLength(8192)
                    .HasColumnType("character varying(8192)")
                    .HasColumnName("Rbac_RoleIdsJson");

                b1.Property<string>("TenantId")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("Rbac_TenantId");

                b1.HasKey("AuditEntryEntitySequence");
                b1.ToTable("AuditEntries");
                b1.WithOwner().HasForeignKey("AuditEntryEntitySequence");
            });

            b.Navigation("RbacMetadata").IsRequired();
        });
#pragma warning restore 612, 618
    }
}
