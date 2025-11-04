using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sakin.Correlation.Persistence.Migrations;

public partial class CreateAlertsTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "alerts",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                rule_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                rule_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                triggered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                correlation_context = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                matched_conditions = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                aggregation_count = table.Column<int>(type: "integer", nullable: true),
                aggregated_value = table.Column<double>(type: "double precision", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_alerts", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_alerts_severity",
            schema: "public",
            table: "alerts",
            column: "severity");

        migrationBuilder.CreateIndex(
            name: "ix_alerts_triggered_at",
            schema: "public",
            table: "alerts",
            column: "triggered_at");

        migrationBuilder.CreateIndex(
            name: "ix_alerts_severity_triggered_at",
            schema: "public",
            table: "alerts",
            columns: new[] { "severity", "triggered_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "alerts",
            schema: "public");
    }
}
