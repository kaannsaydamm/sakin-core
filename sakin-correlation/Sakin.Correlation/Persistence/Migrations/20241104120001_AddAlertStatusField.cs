using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sakin.Correlation.Persistence.Migrations;

public partial class AddAlertStatusField : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "status",
            schema: "public",
            table: "alerts",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "new");

        migrationBuilder.CreateIndex(
            name: "ix_alerts_status",
            schema: "public",
            table: "alerts",
            column: "status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_alerts_status",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "status",
            schema: "public",
            table: "alerts");
    }
}
