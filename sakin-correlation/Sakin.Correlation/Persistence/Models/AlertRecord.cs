using System;
using System.Collections.Generic;
using Sakin.Correlation.Models;

namespace Sakin.Correlation.Persistence.Models;

public class AlertRecord
{
    public Guid Id { get; set; }

    public string RuleId { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public SeverityLevel Severity { get; set; }

    public AlertStatus Status { get; set; }

    public DateTimeOffset TriggeredAt { get; set; }

    public string? Source { get; set; }

    public Dictionary<string, object?> Context { get; set; } = new();

    public IReadOnlyList<string> MatchedConditions { get; set; } = Array.Empty<string>();

    public int? AggregationCount { get; set; }

    public double? AggregatedValue { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
