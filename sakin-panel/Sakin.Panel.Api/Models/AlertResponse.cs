using System.Text.Json.Serialization;
using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Models;

namespace Sakin.Panel.Api.Models;

public record AlertResponse(
    Guid Id,
    string RuleId,
    string RuleName,
    string Severity,
    string Status,
    DateTimeOffset TriggeredAt,
    string? Source,
    Dictionary<string, object?> Context,
    IReadOnlyList<string> MatchedConditions,
    int? AggregationCount,
    double? AggregatedValue,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int AlertCount,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    IReadOnlyList<StatusHistoryEntryDto> StatusHistory,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? InvestigationStartedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? FalsePositiveAt,
    string? ResolutionComment,
    string? ResolutionReason,
    string? DedupKey)
{
    public static AlertResponse FromRecord(AlertRecord alert)
    {
        return new AlertResponse(
            alert.Id,
            alert.RuleId,
            alert.RuleName,
            alert.Severity.ToString().ToLowerInvariant(),
            alert.Status.ToString().ToLowerInvariant(),
            alert.TriggeredAt,
            alert.Source,
            new Dictionary<string, object?>(alert.Context),
            alert.MatchedConditions.ToArray(),
            alert.AggregationCount,
            alert.AggregatedValue,
            alert.CreatedAt,
            alert.UpdatedAt,
            alert.AlertCount,
            alert.FirstSeen,
            alert.LastSeen,
            alert.StatusHistory
                .Select(h => new StatusHistoryEntryDto(
                    h.Timestamp,
                    h.OldStatus,
                    h.NewStatus,
                    h.User,
                    h.Comment))
                .ToList(),
            alert.AcknowledgedAt,
            alert.InvestigationStartedAt,
            alert.ResolvedAt,
            alert.ClosedAt,
            alert.FalsePositiveAt,
            alert.ResolutionComment,
            alert.ResolutionReason,
            alert.DedupKey);
    }
}

public record StatusHistoryEntryDto(
    DateTimeOffset Timestamp,
    string OldStatus,
    string NewStatus,
    string? User,
    string? Comment);
