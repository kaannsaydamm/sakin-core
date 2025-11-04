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

            b.HasKey("Id")
                .HasName("PK_alerts");

            b.HasIndex("Severity")
                .HasDatabaseName("ix_alerts_severity");

            b.HasIndex("TriggeredAt")
                .HasDatabaseName("ix_alerts_triggered_at");

            b.HasIndex("Severity", "TriggeredAt")
                .HasDatabaseName("ix_alerts_severity_triggered_at");

            b.ToTable("alerts", "public");
        });
    }
}
