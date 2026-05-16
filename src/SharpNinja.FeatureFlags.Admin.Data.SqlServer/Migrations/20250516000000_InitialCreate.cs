using Microsoft.EntityFrameworkCore.Migrations;

namespace SharpNinja.FeatureFlags.Admin.Data.SqlServer.Migrations;

/// <summary>FR-9 FR-11: Initial schema migration creating the FlagDrafts table in SQL Server.</summary>
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
                    .Annotation("SqlServer:Identity", "1, 1"),
                FlagKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                EnvironmentName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                ProductScopeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ValueType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                DefaultValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                RuleDescriptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                LastReason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                LastRbac_TenantId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                LastRbac_PrincipalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                LastRbac_ProductIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                LastRbac_RoleIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Revision = table.Column<long>(type: "bigint", nullable: false),
                LastModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
