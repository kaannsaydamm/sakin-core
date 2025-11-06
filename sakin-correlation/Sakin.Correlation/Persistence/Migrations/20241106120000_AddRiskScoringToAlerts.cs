using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sakin.Correlation.Persistence.Migrations;

public partial class AddRiskScoringToAlerts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "risk_score",
            schema: "public",
            table: "alerts",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "risk_level",
            schema: "public",
            table: "alerts",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "low");

        migrationBuilder.AddColumn<string>(
            name: "risk_factors",
            schema: "public",
            table: "alerts",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "reasoning",
            schema: "public",
            table: "alerts",
            type: "text",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_alerts_risk_score",
            schema: "public",
            table: "alerts",
            column: "risk_score");

        migrationBuilder.CreateIndex(
            name: "ix_alerts_risk_level",
            schema: "public",
            table: "alerts",
            column: "risk_level");

        migrationBuilder.CreateIndex(
            name: "ix_alerts_risk_score_triggered_at",
            schema: "public",
            table: "alerts",
            columns: new[] { "risk_score", "triggered_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_alerts_risk_score_triggered_at",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropIndex(
            name: "ix_alerts_risk_level",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropIndex(
            name: "ix_alerts_risk_score",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "reasoning",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "risk_factors",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "risk_level",
            schema: "public",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "risk_score",
            schema: "public",
            table: "alerts");
    }
}