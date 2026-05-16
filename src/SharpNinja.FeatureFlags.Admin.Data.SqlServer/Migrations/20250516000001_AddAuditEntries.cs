using Microsoft.EntityFrameworkCore.Migrations;

namespace SharpNinja.FeatureFlags.Admin.Data.SqlServer.Migrations;

/// <summary>FR-9 FR-11: Adds the AuditEntries table to the SQL Server admin schema.</summary>
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
                    .Annotation("SqlServer:Identity", "1, 1"),
                Action = table.Column<int>(type: "int", nullable: false),
                FlagKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                EnvironmentName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                TargetEnvironmentName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                ProductScopeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ValueType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                DefaultValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                RuleDescriptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                Rbac_TenantId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Rbac_PrincipalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Rbac_ProductIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Rbac_RoleIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Revision = table.Column<long>(type: "bigint", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
