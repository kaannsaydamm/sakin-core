using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sakin.Correlation.Persistence.Migrations;

public partial class UpdateAlertsLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "alert_count",
            schema: "public",
            table: "alerts",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<string>(
            name: "first_seen",
            schema: "public",
            table: "alerts",
            type: "timestamp with time zone",
            nullable: false,
            defaultValueSql: "CURRENT_TIMESTAMP");

        migrationBuilder.AddColumn<string>(
            name: "last_seen",
            schema: "public",
            table: "alerts",
            type: "timestamp with time zone",
            nullable: false,
            defaultValueSql: "CURRENT_TIMESTAMP");

        migrationBuilder.AddColumn<string>(
            name: "status_history",
            schema: "public",
            table: "alerts",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'[]'::jsonb");

        migrationBuilder.AddColumn<string>(
            name: "acknowledged_at",
            schema: "public",
            table: "alerts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "investigation_started_at",
            schema: "public",
            table: "alerts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "resolved_at",
            schema: "public",
            table: "alerts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "closed_at",
            schema: "public",
            table: "alerts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "false_positive_at",
            schema: "public",
            table: "alerts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "resolution_comment",
            schema: "public",
            table: "alerts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "resolution_reason",
            schema: "public",
            table: "alerts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "dedup_key",
            schema: "public",
            table: "alerts",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        // Create indexes
        migrationBuilder.CreateIndex(
            name: "ix_alerts_ruleid_lastseen",
            schema: "public",
            table: "alerts",
            columns: new[] { "rule_id", "last_seen" });

        migrationBuilder.CreateIndex(
            name: "ix_alerts_dedup_key",
            schema: "public",
            table: "alerts",
            column: "dedup_key");

        migrationBuilder.CreateIndex(
            name: "ix_alerts_status_severity",
            schema: "public",
            table: "alerts",
            columns: new[] { "status", "severity" });

        migrationBuilder.CreateIndex(
            name: "ix_alerts_status_history_gin",
            schema: "public",
            table: "alerts",
            column: "status_history",
            oldColumnName: "status_history",
            oldColumnType: "jsonb",
            oldNullable: false)
            .Annotation("Npgsql:IndexMethod", "gin");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_alerts_ruleid_lastseen",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropIndex(
            name: "ix_alerts_dedup_key",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropIndex(
            name: "ix_alerts_status_severity",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropIndex(
            name: "ix_alerts_status_history_gin",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "alert_count",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "first_seen",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "last_seen",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "status_history",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "acknowledged_at",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "investigation_started_at",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "resolved_at",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "closed_at",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "false_positive_at",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "resolution_comment",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "resolution_reason",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "dedup_key",
            schema: "public",
            table: "alerts");
    }
}
