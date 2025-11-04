using Microsoft.Extensions.Logging;
using Sakin.Common.Models;
using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services;

public class RuleEngine : IRuleEngine
{
    private readonly IRuleProvider _ruleProvider;
    private readonly IStateManager _stateManager;
    private readonly ILogger<RuleEngine> _logger;
    private IReadOnlyList<CorrelationRule> _rules = new List<CorrelationRule>();
    private DateTime _lastRuleLoadTime = DateTime.MinValue;
    private readonly TimeSpan _ruleReloadInterval = TimeSpan.FromMinutes(5);

    public RuleEngine(IRuleProvider ruleProvider, IStateManager stateManager, ILogger<RuleEngine> logger)
    {
        _ruleProvider = ruleProvider;
        _stateManager = stateManager;
        _logger = logger;
    }

    public async Task<IEnumerable<Alert>> EvaluateEventAsync(EventEnvelope eventEnvelope, CancellationToken cancellationToken)
    {
        await EnsureRulesLoadedAsync(cancellationToken);

        if (eventEnvelope.Normalized == null)
        {
            _logger.LogWarning("Event {EventId} has no normalized data. Skipping rule evaluation.", eventEnvelope.EventId);
            return Array.Empty<Alert>();
        }

        var alerts = new List<Alert>();
        foreach (var rule in _rules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

            if (!MatchesConditions(eventEnvelope.Normalized, rule))
            {
                continue;
            }

            var groupKey = BuildGroupKey(eventEnvelope.Normalized, rule);
            var expiration = TimeSpan.FromSeconds(rule.TimeWindowSeconds);

            var state = await _stateManager.AddEventAsync(rule.Id, groupKey, eventEnvelope.EventId, expiration, cancellationToken);

            if (state.EventCount >= rule.MinEventCount)
            {
                var alert = BuildAlert(rule, state, eventEnvelope.Normalized);
                alerts.Add(alert);

                _logger.LogInformation(
                    "Rule {RuleId} triggered for group {GroupKey}. Event count: {EventCount}",
                    rule.Id, groupKey, state.EventCount);

                await _stateManager.ClearGroupAsync(rule.Id, groupKey, cancellationToken);
            }
            else
            {
                _logger.LogDebug(
                    "Rule {RuleId} event count {EventCount}/{MinCount} for group {GroupKey}",
                    rule.Id, state.EventCount, rule.MinEventCount, groupKey);
            }
        }

        return alerts;
    }

    private async Task EnsureRulesLoadedAsync(CancellationToken cancellationToken)
    {
        if (_rules.Count == 0 || DateTime.UtcNow - _lastRuleLoadTime > _ruleReloadInterval)
        {
            _rules = await _ruleProvider.GetRulesAsync(cancellationToken);
            _lastRuleLoadTime = DateTime.UtcNow;
            _logger.LogInformation("Loaded {RuleCount} correlation rules", _rules.Count);
        }
    }

    private static bool MatchesConditions(NormalizedEvent normalizedEvent, CorrelationRule rule)
    {
        if (rule.Conditions.Count == 0)
        {
            return true;
        }

        foreach (var condition in rule.Conditions)
        {
            var fieldValue = GetFieldValue(normalizedEvent, condition.Field);
            if (!EvaluateCondition(fieldValue, condition))
            {
                return false;
            }
        }

        return true;
    }

    private static string? GetFieldValue(NormalizedEvent normalizedEvent, string field)
    {
        return field switch
        {
            "EventType" => normalizedEvent.EventType.ToString(),
            "Severity" => normalizedEvent.Severity.ToString(),
            "SourceIp" => normalizedEvent.SourceIp,
            "DestinationIp" => normalizedEvent.DestinationIp,
            "Protocol" => normalizedEvent.Protocol.ToString(),
            "DeviceName" => normalizedEvent.DeviceName,
            "SensorId" => normalizedEvent.SensorId,
            _ => normalizedEvent.Metadata.TryGetValue(field, out var value) ? value?.ToString() : null
        };
    }

    private static bool EvaluateCondition(string? fieldValue, RuleCondition condition)
    {
        if (fieldValue == null)
        {
            return false;
        }

        return condition.Operator switch
        {
            RuleOperator.Equals => string.Equals(fieldValue, condition.Value, StringComparison.OrdinalIgnoreCase),
            RuleOperator.Contains => fieldValue.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
            RuleOperator.StartsWith => fieldValue.StartsWith(condition.Value, StringComparison.OrdinalIgnoreCase),
            RuleOperator.EndsWith => fieldValue.EndsWith(condition.Value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static string BuildGroupKey(NormalizedEvent normalizedEvent, CorrelationRule rule)
    {
        if (rule.GroupByFields.Count == 0)
        {
            return normalizedEvent.SourceIp ?? "default";
        }

        var keyParts = new List<string>();
        foreach (var field in rule.GroupByFields)
        {
            var value = GetFieldValue(normalizedEvent, field);
            keyParts.Add(value ?? "null");
        }

        return string.Join(":", keyParts);
    }

    private static Alert BuildAlert(CorrelationRule rule, CorrelationState state, NormalizedEvent latestEvent)
    {
        return new Alert
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            RuleName = rule.Name,
            RuleId = rule.Id,
            Severity = rule.Severity,
            Title = rule.Name,
            Description = rule.Description,
            EventIds = state.EventIds,
            EventCount = state.EventCount,
            SourceIp = latestEvent.SourceIp,
            DestinationIp = latestEvent.DestinationIp,
            Tags = rule.Tags,
            Metadata = new Dictionary<string, object>
            {
                ["timeWindowSeconds"] = rule.TimeWindowSeconds,
                ["minEventCount"] = rule.MinEventCount,
                ["latestEventId"] = latestEvent.Id
            }
        };
    }
}
