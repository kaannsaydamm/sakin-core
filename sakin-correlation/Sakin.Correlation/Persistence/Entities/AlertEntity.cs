using System;

namespace Sakin.Correlation.Persistence.Entities;

public class AlertEntity
{
    public Guid Id { get; set; }

    public string RuleId { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Status { get; set; } = "new";

    public DateTimeOffset TriggeredAt { get; set; }

    public string? Source { get; set; }

    public string CorrelationContext { get; set; } = "{}";

    public string MatchedConditions { get; set; } = "[]";

    public int? AggregationCount { get; set; }

    public double? AggregatedValue { get; set; }

    public int RiskScore { get; set; } = 0;

    public string RiskLevel { get; set; } = "low";

    public string? RiskFactors { get; set; }

    public string? Reasoning { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    // Lifecycle fields
    public int AlertCount { get; set; } = 1;

    public DateTimeOffset FirstSeen { get; set; }

    public DateTimeOffset LastSeen { get; set; }

    public string StatusHistory { get; set; } = "[]";

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public DateTimeOffset? InvestigationStartedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public DateTimeOffset? FalsePositiveAt { get; set; }

    public string? ResolutionComment { get; set; }

    public string? ResolutionReason { get; set; }

    public string? DedupKey { get; set; }
}
