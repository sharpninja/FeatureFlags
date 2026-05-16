using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace SharpNinja.FeatureFlags.Admin.Data.Postgres.Migrations;

/// <summary>FR-9 FR-11: Initial schema migration creating the FlagDrafts table in PostgreSQL.</summary>
#pragma warning disable CA1062 // migrationBuilder nullability checked by EF Core caller
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "FlagDrafts",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                FlagKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                EnvironmentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ProductScopeJson = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                ValueType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                DefaultValue = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                RuleDescriptionsJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                LastReason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                LastRbac_TenantId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                LastRbac_PrincipalId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                LastRbac_ProductIdsJson = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                LastRbac_RoleIdsJson = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                Revision = table.Column<long>(type: "bigint", nullable: false),
                LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FlagDrafts", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FlagDrafts_FlagKey_EnvironmentName",
            table: "FlagDrafts",
            columns: ["FlagKey", "EnvironmentName"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(name: "FlagDrafts");
    }
}
