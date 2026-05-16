using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace SharpNinja.FeatureFlags.Admin.Data.Postgres.Migrations;

/// <summary>FR-9 FR-11: Adds the AuditEntries table to the PostgreSQL admin schema.</summary>
#pragma warning disable CA1062 // migrationBuilder nullability checked by EF Core caller
public partial class AddAuditEntries : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "AuditEntries",
            columns: table => new
            {
                Sequence = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Action = table.Column<int>(type: "integer", nullable: false),
                FlagKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                EnvironmentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                TargetEnvironmentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                ProductScopeJson = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                ValueType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                DefaultValue = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                RuleDescriptionsJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                Reason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                Rbac_TenantId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Rbac_PrincipalId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Rbac_ProductIdsJson = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                Rbac_RoleIdsJson = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                Revision = table.Column<long>(type: "bigint", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditEntries", x => x.Sequence);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(name: "AuditEntries");
    }
}
