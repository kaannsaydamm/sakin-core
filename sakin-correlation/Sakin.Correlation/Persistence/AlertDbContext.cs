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

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.Severity)
                .HasDatabaseName("ix_alerts_severity");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("ix_alerts_status");

            entity.HasIndex(e => e.TriggeredAt)
                .HasDatabaseName("ix_alerts_triggered_at");

            entity.HasIndex(e => new { e.Severity, e.TriggeredAt })
                .HasDatabaseName("ix_alerts_severity_triggered_at");
        });
    }
}
