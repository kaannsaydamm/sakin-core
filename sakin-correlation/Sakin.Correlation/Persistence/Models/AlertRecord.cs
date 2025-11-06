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

    // Lifecycle fields
    public int AlertCount { get; set; } = 1;

    public DateTimeOffset FirstSeen { get; set; }

    public DateTimeOffset LastSeen { get; set; }

    public IReadOnlyList<StatusHistoryEntry> StatusHistory { get; set; } = Array.Empty<StatusHistoryEntry>();

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public DateTimeOffset? InvestigationStartedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public DateTimeOffset? FalsePositiveAt { get; set; }

    public string? ResolutionComment { get; set; }

    public string? ResolutionReason { get; set; }

    public string? DedupKey { get; set; }
}

public class StatusHistoryEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string? User { get; set; }
    public string? Comment { get; set; }
}
