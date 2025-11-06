using Microsoft.EntityFrameworkCore;
using Sakin.Correlation.Persistence.Entities;

namespace Sakin.Correlation.Persistence;

public class AlertDbContext : DbContext
{
    public AlertDbContext(DbContextOptions<AlertDbContext> options)
        : base(options)
    {
    }

    public DbSet<AlertEntity> Alerts => Set<AlertEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<AlertEntity>(entity =>
        {
            entity.ToTable("alerts");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.RuleId)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(e => e.RuleName)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.Severity)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(32)
                .HasDefaultValue("new");

            entity.Property(e => e.TriggeredAt)
                .IsRequired();

            entity.Property(e => e.Source)
                .HasMaxLength(128);

            entity.Property(e => e.CorrelationContext)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'{}'::jsonb");

            entity.Property(e => e.MatchedConditions)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'[]'::jsonb");

            entity.Property(e => e.AggregationCount);

            entity.Property(e => e.AggregatedValue);

            entity.Property(e => e.RiskScore)
                .HasDefaultValue(0);

            entity.Property(e => e.RiskLevel)
                .IsRequired()
                .HasMaxLength(32)
                .HasDefaultValue("low");

            entity.Property(e => e.RiskFactors)
                .HasColumnType("jsonb");

            entity.Property(e => e.Reasoning)
                .HasColumnType("text");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Lifecycle fields
            entity.Property(e => e.AlertCount)
                .HasDefaultValue(1);

            entity.Property(e => e.FirstSeen)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.LastSeen)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.StatusHistory)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'[]'::jsonb");

            entity.Property(e => e.AcknowledgedAt);
            entity.Property(e => e.InvestigationStartedAt);
            entity.Property(e => e.ResolvedAt);
            entity.Property(e => e.ClosedAt);
            entity.Property(e => e.FalsePositiveAt);

            entity.Property(e => e.ResolutionComment)
                .HasColumnType("text");

            entity.Property(e => e.ResolutionReason)
                .HasColumnType("text");

            entity.Property(e => e.DedupKey)
                .HasMaxLength(256);

            // Indexes
            entity.HasIndex(e => e.Severity)
                .HasDatabaseName("ix_alerts_severity");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("ix_alerts_status");

            entity.HasIndex(e => e.TriggeredAt)
                .HasDatabaseName("ix_alerts_triggered_at");

            entity.HasIndex(e => new { e.Severity, e.TriggeredAt })
                .HasDatabaseName("ix_alerts_severity_triggered_at");

            entity.HasIndex(e => e.RiskScore)
                .HasDatabaseName("ix_alerts_risk_score");

            entity.HasIndex(e => e.RiskLevel)
                .HasDatabaseName("ix_alerts_risk_level");

            entity.HasIndex(e => new { e.RiskScore, e.TriggeredAt })
                .HasDatabaseName("ix_alerts_risk_score_triggered_at");

            // Lifecycle indexes
            entity.HasIndex(e => new { e.RuleId, e.LastSeen })
                .HasDatabaseName("ix_alerts_ruleid_lastseen");

            entity.HasIndex(e => e.DedupKey)
                .HasDatabaseName("ix_alerts_dedup_key");

            entity.HasIndex(e => new { e.Status, e.Severity })
                .HasDatabaseName("ix_alerts_status_severity");

            // JSONB GIN index for status history
            entity.HasIndex("StatusHistory")
                .HasDatabaseName("ix_alerts_status_history_gin")
                .HasMethod("gin");
        });
    }
}
