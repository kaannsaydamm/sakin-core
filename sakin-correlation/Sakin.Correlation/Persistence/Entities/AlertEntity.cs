using System;

namespace Sakin.Correlation.Persistence.Entities;

public class AlertEntity
{
    public Guid Id { get; set; }

    public string RuleId { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public DateTimeOffset TriggeredAt { get; set; }

    public string? Source { get; set; }

    public string CorrelationContext { get; set; } = "{}";

    public string MatchedConditions { get; set; } = "[]";

    public int? AggregationCount { get; set; }

    public double? AggregatedValue { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
