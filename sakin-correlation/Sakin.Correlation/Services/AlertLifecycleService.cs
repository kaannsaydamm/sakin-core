using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Models;
using Sakin.Correlation.Persistence.Repositories;

namespace Sakin.Correlation.Services;

public interface IAlertLifecycleService
{
    Task<AlertRecord?> TransitionStatusAsync(
        Guid alertId,
        AlertStatus newStatus,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default);

    Task<AlertRecord?> AcknowledgeAsync(
        Guid alertId,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default);

    Task<AlertRecord?> StartInvestigationAsync(
        Guid alertId,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default);

    Task<AlertRecord?> ResolveAsync(
        Guid alertId,
        string? reason = null,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default);

    Task<AlertRecord?> CloseAsync(
        Guid alertId,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default);

    Task<AlertRecord?> MarkFalsePositiveAsync(
        Guid alertId,
        string? reason = null,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default);
}

public class AlertLifecycleService : IAlertLifecycleService
{
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<AlertLifecycleService> _logger;

    private static readonly Dictionary<AlertStatus, HashSet<AlertStatus>> AllowedTransitions = new()
    {
        { AlertStatus.New, new HashSet<AlertStatus> { AlertStatus.Acknowledged, AlertStatus.PendingScore, AlertStatus.FalsePositive } },
        { AlertStatus.PendingScore, new HashSet<AlertStatus> { AlertStatus.New, AlertStatus.Acknowledged, AlertStatus.FalsePositive } },
        { AlertStatus.Acknowledged, new HashSet<AlertStatus> { AlertStatus.UnderInvestigation, AlertStatus.Resolved, AlertStatus.FalsePositive } },
        { AlertStatus.UnderInvestigation, new HashSet<AlertStatus> { AlertStatus.Resolved, AlertStatus.Closed, AlertStatus.FalsePositive } },
        { AlertStatus.Resolved, new HashSet<AlertStatus> { AlertStatus.Closed, AlertStatus.Acknowledged } },
        { AlertStatus.Closed, new HashSet<AlertStatus> { AlertStatus.Acknowledged } },
        { AlertStatus.FalsePositive, new HashSet<AlertStatus> { AlertStatus.Closed } }
    };

    public AlertLifecycleService(
        IAlertRepository alertRepository,
        ILogger<AlertLifecycleService> logger)
    {
        _alertRepository = alertRepository;
        _logger = logger;
    }

    public async Task<AlertRecord?> TransitionStatusAsync(
        Guid alertId,
        AlertStatus newStatus,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken);
        if (alert is null)
        {
            _logger.LogWarning("Alert {AlertId} not found for status transition", alertId);
            return null;
        }

        var oldStatus = alert.Status;

        if (!IsTransitionAllowed(oldStatus, newStatus))
        {
            _logger.LogWarning(
                "Invalid status transition for alert {AlertId}: {OldStatus} -> {NewStatus}",
                alertId, oldStatus, newStatus);
            throw new InvalidOperationException(
                $"Cannot transition from {oldStatus} to {newStatus}");
        }

        var statusHistoryEntry = new StatusHistoryEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            OldStatus = oldStatus.ToString().ToLowerInvariant(),
            NewStatus = newStatus.ToString().ToLowerInvariant(),
            User = user,
            Comment = comment
        };

        var updatedHistory = new List<StatusHistoryEntry>(alert.StatusHistory) { statusHistoryEntry };

        var entity = new Entities.AlertEntity
        {
            Id = alert.Id,
            RuleId = alert.RuleId,
            RuleName = alert.RuleName,
            Severity = alert.Severity.ToString().ToLowerInvariant(),
            Status = newStatus.ToString().ToLowerInvariant(),
            TriggeredAt = alert.TriggeredAt,
            Source = alert.Source,
            CorrelationContext = JsonSerializer.Serialize(alert.Context),
            MatchedConditions = JsonSerializer.Serialize(alert.MatchedConditions),
            AggregationCount = alert.AggregationCount,
            AggregatedValue = alert.AggregatedValue,
            CreatedAt = alert.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
            AlertCount = alert.AlertCount,
            FirstSeen = alert.FirstSeen,
            LastSeen = alert.LastSeen,
            StatusHistory = JsonSerializer.Serialize(updatedHistory),
            AcknowledgedAt = newStatus == AlertStatus.Acknowledged ? DateTimeOffset.UtcNow : alert.AcknowledgedAt,
            InvestigationStartedAt = newStatus == AlertStatus.UnderInvestigation ? DateTimeOffset.UtcNow : alert.InvestigationStartedAt,
            ResolvedAt = newStatus == AlertStatus.Resolved ? DateTimeOffset.UtcNow : alert.ResolvedAt,
            ClosedAt = newStatus == AlertStatus.Closed ? DateTimeOffset.UtcNow : alert.ClosedAt,
            FalsePositiveAt = newStatus == AlertStatus.FalsePositive ? DateTimeOffset.UtcNow : alert.FalsePositiveAt,
            ResolutionComment = comment ?? alert.ResolutionComment,
            ResolutionReason = alert.ResolutionReason,
            DedupKey = alert.DedupKey
        };

        await _alertRepository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation(
            "Alert {AlertId} transitioned from {OldStatus} to {NewStatus} by {User}",
            alertId, oldStatus, newStatus, user ?? "system");

        return await _alertRepository.GetByIdAsync(alertId, cancellationToken);
    }

    public async Task<AlertRecord?> AcknowledgeAsync(
        Guid alertId,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default)
    {
        return await TransitionStatusAsync(alertId, AlertStatus.Acknowledged, comment, user, cancellationToken);
    }

    public async Task<AlertRecord?> StartInvestigationAsync(
        Guid alertId,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default)
    {
        return await TransitionStatusAsync(alertId, AlertStatus.UnderInvestigation, comment, user, cancellationToken);
    }

    public async Task<AlertRecord?> ResolveAsync(
        Guid alertId,
        string? reason = null,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken);
        if (alert is null)
        {
            return null;
        }

        var oldStatus = alert.Status;
        var statusHistoryEntry = new StatusHistoryEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            OldStatus = oldStatus.ToString().ToLowerInvariant(),
            NewStatus = AlertStatus.Resolved.ToString().ToLowerInvariant(),
            User = user,
            Comment = comment
        };

        var updatedHistory = new List<StatusHistoryEntry>(alert.StatusHistory) { statusHistoryEntry };

        var entity = new Entities.AlertEntity
        {
            Id = alert.Id,
            RuleId = alert.RuleId,
            RuleName = alert.RuleName,
            Severity = alert.Severity.ToString().ToLowerInvariant(),
            Status = AlertStatus.Resolved.ToString().ToLowerInvariant(),
            TriggeredAt = alert.TriggeredAt,
            Source = alert.Source,
            CorrelationContext = JsonSerializer.Serialize(alert.Context),
            MatchedConditions = JsonSerializer.Serialize(alert.MatchedConditions),
            AggregationCount = alert.AggregationCount,
            AggregatedValue = alert.AggregatedValue,
            CreatedAt = alert.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
            AlertCount = alert.AlertCount,
            FirstSeen = alert.FirstSeen,
            LastSeen = alert.LastSeen,
            StatusHistory = JsonSerializer.Serialize(updatedHistory),
            AcknowledgedAt = alert.AcknowledgedAt,
            InvestigationStartedAt = alert.InvestigationStartedAt,
            ResolvedAt = DateTimeOffset.UtcNow,
            ClosedAt = alert.ClosedAt,
            FalsePositiveAt = alert.FalsePositiveAt,
            ResolutionComment = comment,
            ResolutionReason = reason,
            DedupKey = alert.DedupKey
        };

        await _alertRepository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation(
            "Alert {AlertId} resolved by {User} with reason: {Reason}",
            alertId, user ?? "system", reason ?? "none");

        return await _alertRepository.GetByIdAsync(alertId, cancellationToken);
    }

    public async Task<AlertRecord?> CloseAsync(
        Guid alertId,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default)
    {
        return await TransitionStatusAsync(alertId, AlertStatus.Closed, comment, user, cancellationToken);
    }

    public async Task<AlertRecord?> MarkFalsePositiveAsync(
        Guid alertId,
        string? reason = null,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken);
        if (alert is null)
        {
            return null;
        }

        var oldStatus = alert.Status;
        var statusHistoryEntry = new StatusHistoryEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            OldStatus = oldStatus.ToString().ToLowerInvariant(),
            NewStatus = AlertStatus.FalsePositive.ToString().ToLowerInvariant(),
            User = user,
            Comment = comment
        };

        var updatedHistory = new List<StatusHistoryEntry>(alert.StatusHistory) { statusHistoryEntry };

        var entity = new Entities.AlertEntity
        {
            Id = alert.Id,
            RuleId = alert.RuleId,
            RuleName = alert.RuleName,
            Severity = alert.Severity.ToString().ToLowerInvariant(),
            Status = AlertStatus.FalsePositive.ToString().ToLowerInvariant(),
            TriggeredAt = alert.TriggeredAt,
            Source = alert.Source,
            CorrelationContext = JsonSerializer.Serialize(alert.Context),
            MatchedConditions = JsonSerializer.Serialize(alert.MatchedConditions),
            AggregationCount = alert.AggregationCount,
            AggregatedValue = alert.AggregatedValue,
            CreatedAt = alert.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
            AlertCount = alert.AlertCount,
            FirstSeen = alert.FirstSeen,
            LastSeen = alert.LastSeen,
            StatusHistory = JsonSerializer.Serialize(updatedHistory),
            AcknowledgedAt = alert.AcknowledgedAt,
            InvestigationStartedAt = alert.InvestigationStartedAt,
            ResolvedAt = alert.ResolvedAt,
            ClosedAt = alert.ClosedAt,
            FalsePositiveAt = DateTimeOffset.UtcNow,
            ResolutionComment = comment,
            ResolutionReason = reason,
            DedupKey = alert.DedupKey
        };

        await _alertRepository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation(
            "Alert {AlertId} marked as false positive by {User} with reason: {Reason}",
            alertId, user ?? "system", reason ?? "none");

        return await _alertRepository.GetByIdAsync(alertId, cancellationToken);
    }

    private static bool IsTransitionAllowed(AlertStatus from, AlertStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return AllowedTransitions.TryGetValue(from, out var allowedStates) && 
               allowedStates.Contains(to);
    }
}
