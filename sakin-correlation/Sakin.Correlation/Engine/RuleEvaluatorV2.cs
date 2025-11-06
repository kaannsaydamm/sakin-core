using Microsoft.Extensions.Logging;
using Sakin.Correlation.Models;
using Sakin.Correlation.Services;
using Sakin.Common.Models;
using System.Text.Json;

namespace Sakin.Correlation.Engine;

public class RuleEvaluatorV2 : IRuleEvaluatorV2
{
    private readonly ILogger<RuleEvaluatorV2> _logger;
    private readonly IAggregationEvaluator _aggregationEvaluator;
    private readonly IRuleEvaluator _legacyRuleEvaluator;

    public RuleEvaluatorV2(
        ILogger<RuleEvaluatorV2> logger,
        IAggregationEvaluator aggregationEvaluator,
        IRuleEvaluator legacyRuleEvaluator)
    {
        _logger = logger;
        _aggregationEvaluator = aggregationEvaluator;
        _legacyRuleEvaluator = legacyRuleEvaluator;
    }

    public async Task<EvaluationResult> EvaluateAsync(CorrelationRule rule, EventEnvelope eventEnvelope)
    {
        // Delegate to legacy evaluator for old format
        return await _legacyRuleEvaluator.EvaluateAsync(rule, eventEnvelope);
    }

    public async Task<EvaluationResult> EvaluateAsync(CorrelationRuleV2 rule, EventEnvelope eventEnvelope)
    {
        if (!rule.Enabled)
        {
            return new EvaluationResult
            {
                IsMatch = false,
                ShouldTriggerAlert = false,
                Reason = "Rule is disabled"
            };
        }

        if (eventEnvelope.Normalized is null)
        {
            return new EvaluationResult
            {
                IsMatch = false,
                ShouldTriggerAlert = false,
                Reason = "Event has no normalized data"
            };
        }

        // Check if event matches trigger criteria
        if (!MatchesTrigger(rule.Trigger, eventEnvelope))
        {
            return new EvaluationResult
            {
                IsMatch = false,
                ShouldTriggerAlert = false,
                Reason = "Event did not match trigger criteria"
            };
        }

        // Check basic condition (if any)
        if (!string.IsNullOrEmpty(rule.Condition.Field) && rule.Condition.Aggregation == null)
        {
            var fieldValue = GetFieldValue(eventEnvelope, rule.Condition.Field);
            var conditionMet = EvaluateBasicCondition(rule.Condition, fieldValue);
            
            if (!conditionMet)
            {
                return new EvaluationResult
                {
                    IsMatch = false,
                    ShouldTriggerAlert = false,
                    Reason = $"Condition failed: {rule.Condition.Field} {rule.Condition.Operator} {rule.Condition.Value}"
                };
            }
        }

        // If aggregation condition exists, use aggregation evaluator
        if (rule.Condition.Aggregation != null)
        {
            var aggregationResult = await _aggregationEvaluator.EvaluateAggregationAsync(rule, eventEnvelope);
            
            return new EvaluationResult
            {
                IsMatch = true,
                ShouldTriggerAlert = aggregationResult,
                Reason = aggregationResult 
                    ? "Aggregation threshold reached"
                    : "Aggregation threshold not reached",
                Context = BuildContext(eventEnvelope)
            };
        }
        else
        {
            // Existing stateless logic
            return new EvaluationResult
            {
                IsMatch = true,
                ShouldTriggerAlert = true,
                Reason = "All conditions matched",
                Context = BuildContext(eventEnvelope)
            };
        }
    }

    public Task<EvaluationResult> EvaluateWithAggregationAsync(CorrelationRule rule, IEnumerable<EventEnvelope> events)
    {
        // Delegate to legacy evaluator for old format
        return _legacyRuleEvaluator.EvaluateWithAggregationAsync(rule, events);
    }

    private bool MatchesTrigger(RuleTrigger trigger, EventEnvelope eventEnvelope)
    {
        if (trigger.SourceTypes == null || trigger.SourceTypes.Count == 0)
        {
            return true;
        }

        // Check if event type matches source types
        var eventType = eventEnvelope.Normalized?.EventType.ToString().ToLowerInvariant();
        if (string.IsNullOrEmpty(eventType))
        {
            return false;
        }

        return trigger.SourceTypes.Any(st => st.ToLowerInvariant() == eventType);
    }

    private bool EvaluateBasicCondition(ConditionWithAggregation condition, object? fieldValue)
    {
        if (condition.Value == null)
        {
            return condition.Operator.ToLowerInvariant() switch
            {
                "exists" => fieldValue != null,
                "not_exists" => fieldValue == null,
                _ => false
            };
        }

        var fieldValueStr = fieldValue?.ToString() ?? string.Empty;
        var conditionValueStr = condition.Value.ToString() ?? string.Empty;

        return condition.Operator.ToLowerInvariant() switch
        {
            "equals" => string.Equals(fieldValueStr, conditionValueStr, StringComparison.OrdinalIgnoreCase),
            "not_equals" => !string.Equals(fieldValueStr, conditionValueStr, StringComparison.OrdinalIgnoreCase),
            "contains" => fieldValueStr.Contains(conditionValueStr, StringComparison.OrdinalIgnoreCase),
            "not_contains" => !fieldValueStr.Contains(conditionValueStr, StringComparison.OrdinalIgnoreCase),
            "starts_with" => fieldValueStr.StartsWith(conditionValueStr, StringComparison.OrdinalIgnoreCase),
            "ends_with" => fieldValueStr.EndsWith(conditionValueStr, StringComparison.OrdinalIgnoreCase),
            "greater_than" => CompareNumericValues(fieldValueStr, conditionValueStr) > 0,
            "greater_than_or_equal" => CompareNumericValues(fieldValueStr, conditionValueStr) >= 0,
            "less_than" => CompareNumericValues(fieldValueStr, conditionValueStr) < 0,
            "less_than_or_equal" => CompareNumericValues(fieldValueStr, conditionValueStr) <= 0,
            "in" => condition.Value is JsonElement array && array.EnumerateArray().Any(item => 
                string.Equals(fieldValueStr, item.ToString(), StringComparison.OrdinalIgnoreCase)),
            "not_in" => condition.Value is JsonElement notInArray && !notInArray.EnumerateArray().Any(item => 
                string.Equals(fieldValueStr, item.ToString(), StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private int CompareNumericValues(string value1, string value2)
    {
        if (double.TryParse(value1, out var num1) && double.TryParse(value2, out var num2))
        {
            return num1.CompareTo(num2);
        }
        return string.Compare(value1, value2, StringComparison.OrdinalIgnoreCase);
    }

    private object? GetFieldValue(EventEnvelope eventEnvelope, string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            return null;
        }

        // Handle dot notation for nested fields (e.g., "Normalized.source_ip")
        if (fieldName.StartsWith("Normalized."))
        {
            var propertyName = fieldName.Substring("Normalized.".Length);
            return GetProperty(eventEnvelope.Normalized, propertyName);
        }

        return GetProperty(eventEnvelope, fieldName);
    }

    private object? GetProperty(object? obj, string propertyName)
    {
        if (obj == null) return null;

        var properties = propertyName.Split('.');
        object current = obj;

        foreach (var prop in properties)
        {
            var property = current.GetType().GetProperty(prop, 
                System.Reflection.BindingFlags.IgnoreCase | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance);
            
            if (property == null) return null;
            
            current = property.GetValue(current);
        }

        return current;
    }

    private Dictionary<string, object> BuildContext(EventEnvelope eventEnvelope)
    {
        return new Dictionary<string, object>
        {
            ["eventId"] = eventEnvelope.EventId,
            ["eventType"] = eventEnvelope.Normalized?.EventType.ToString() ?? "unknown",
            ["timestamp"] = eventEnvelope.Normalized?.Timestamp ?? DateTime.UtcNow,
            ["sourceIp"] = eventEnvelope.Normalized?.SourceIp ?? "unknown",
            ["deviceName"] = eventEnvelope.Normalized?.DeviceName ?? "unknown"
        };
    }
}