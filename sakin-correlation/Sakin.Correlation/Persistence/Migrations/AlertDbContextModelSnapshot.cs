using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Sakin.Correlation.Persistence;

#nullable disable

namespace Sakin.Correlation.Persistence.Migrations;

[DbContext(typeof(AlertDbContext))]
partial class AlertDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.10")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.HasDefaultSchema("public");

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("Sakin.Correlation.Persistence.Entities.AlertEntity", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("id");

            b.Property<double?>("AggregatedValue")
                .HasColumnType("double precision")
                .HasColumnName("aggregated_value");

            b.Property<int?>("AggregationCount")
                .HasColumnType("integer")
                .HasColumnName("aggregation_count");

            b.Property<string>("CorrelationContext")
                .IsRequired()
                .HasColumnType("jsonb")
                .HasColumnName("correlation_context")
                .HasDefaultValueSql("'{}'::jsonb");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            b.Property<string>("MatchedConditions")
                .IsRequired()
                .HasColumnType("jsonb")
                .HasColumnName("matched_conditions")
                .HasDefaultValueSql("'[]'::jsonb");

            b.Property<string>("RuleId")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("rule_id");

            b.Property<string>("RuleName")
                .IsRequired()
                .HasMaxLength(256)
                .HasColumnType("character varying(256)")
                .HasColumnName("rule_name");

            b.Property<string>("Severity")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("character varying(32)")
                .HasColumnName("severity");

            b.Property<string>("Status")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("character varying(32)")
                .HasColumnName("status")
                .HasDefaultValue("new");

            b.Property<string>("Source")
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("source");

            b.Property<DateTimeOffset>("TriggeredAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("triggered_at");

            b.Property<DateTimeOffset>("UpdatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            b.Property<int>("RiskScore")
                .HasColumnType("integer")
                .HasColumnName("risk_score")
                .HasDefaultValue(0);

            b.Property<string>("RiskLevel")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("character varying(32)")
                .HasColumnName("risk_level")
                .HasDefaultValue("low");

            b.Property<string>("RiskFactors")
                .HasColumnType("jsonb")
                .HasColumnName("risk_factors");

            b.Property<string>("Reasoning")
                .HasColumnType("text")
                .HasColumnName("reasoning");

            // Lifecycle fields
            b.Property<int>("AlertCount")
                .HasColumnType("integer")
                .HasColumnName("alert_count")
                .HasDefaultValue(1);

            b.Property<DateTimeOffset>("FirstSeen")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("first_seen")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            b.Property<DateTimeOffset>("LastSeen")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("last_seen")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            b.Property<string>("StatusHistory")
                .IsRequired()
                .HasColumnType("jsonb")
                .HasColumnName("status_history")
                .HasDefaultValueSql("'[]'::jsonb");

            b.Property<DateTimeOffset?>("AcknowledgedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("acknowledged_at");

            b.Property<DateTimeOffset?>("InvestigationStartedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("investigation_started_at");

            b.Property<DateTimeOffset?>("ResolvedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("resolved_at");

            b.Property<DateTimeOffset?>("ClosedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("closed_at");

            b.Property<DateTimeOffset?>("FalsePositiveAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("false_positive_at");

            b.Property<string>("ResolutionComment")
                .HasColumnType("text")
                .HasColumnName("resolution_comment");

            b.Property<string>("ResolutionReason")
                .HasColumnType("text")
                .HasColumnName("resolution_reason");

            b.Property<string>("DedupKey")
                .HasMaxLength(256)
                .HasColumnType("character varying(256)")
                .HasColumnName("dedup_key");

            b.HasKey("Id")
                .HasName("PK_alerts");

            b.HasIndex("Severity")
                .HasDatabaseName("ix_alerts_severity");

            b.HasIndex("Status")
                .HasDatabaseName("ix_alerts_status");

            b.HasIndex("TriggeredAt")
                .HasDatabaseName("ix_alerts_triggered_at");

            b.HasIndex("Severity", "TriggeredAt")
                .HasDatabaseName("ix_alerts_severity_triggered_at");

            b.HasIndex("RiskScore")
                .HasDatabaseName("ix_alerts_risk_score");

            b.HasIndex("RiskLevel")
                .HasDatabaseName("ix_alerts_risk_level");

            b.HasIndex("RiskScore", "TriggeredAt")
                .HasDatabaseName("ix_alerts_risk_score_triggered_at");

            b.HasIndex("RuleId", "LastSeen")
                .HasDatabaseName("ix_alerts_ruleid_lastseen");

            b.HasIndex("DedupKey")
                .HasDatabaseName("ix_alerts_dedup_key");

            b.HasIndex("Status", "Severity")
                .HasDatabaseName("ix_alerts_status_severity");

            b.HasIndex("StatusHistory")
                .HasDatabaseName("ix_alerts_status_history_gin")
                .HasAnnotation("Npgsql:IndexMethod", "gin");

            b.ToTable("alerts", "public");
        });
    }
}
