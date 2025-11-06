using Microsoft.Extensions.Logging;
using Sakin.Correlation.Models;
using Sakin.Correlation.Services;
using Sakin.Common.Models;

namespace Sakin.Correlation.Services;

public interface IAggregationEvaluator
{
    Task<bool> EvaluateAggregationAsync(CorrelationRuleV2 rule, EventEnvelope @event);
}

public class AggregationEvaluatorService : IAggregationEvaluator
{
    private readonly IRedisStateManager _redisStateManager;
    private readonly ILogger<AggregationEvaluatorService> _logger;

    public AggregationEvaluatorService(
        IRedisStateManager redisStateManager,
        ILogger<AggregationEvaluatorService> logger)
    {
        _redisStateManager = redisStateManager;
        _logger = logger;
    }

    public async Task<bool> EvaluateAggregationAsync(CorrelationRuleV2 rule, EventEnvelope @event)
    {
        if (rule.Condition.Aggregation == null)
        {
            _logger.LogWarning("Rule {RuleId} has no aggregation configuration", rule.Id);
            return false;
        }

        try
        {
            // Extract group_by field value
            var groupValue = ExtractGroupValue(@event, rule.Condition.Aggregation.GroupBy);
            
            // Calculate current window ID (sliding window)
            var windowId = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / rule.Condition.Aggregation.WindowSeconds;
            
            _logger.LogDebug("Evaluating aggregation for rule {RuleId}, group {GroupValue}, window {WindowId}", 
                rule.Id, groupValue, windowId);

            // Increment counter in Redis
            var currentCount = await _redisStateManager.IncrementCounterAsync(rule.Id, groupValue, windowId);
            
            // Get total count for current window (same as currentCount since we just incremented)
            var totalCount = await _redisStateManager.GetCountAsync(rule.Id, groupValue, windowId);
            
            // Check if threshold reached
            var threshold = Convert.ToInt64(rule.Condition.Value);
            var thresholdReached = totalCount >= threshold;
            
            _logger.LogInformation("Rule {RuleId}: group {GroupValue} has {Count} events in current window (threshold: {Threshold}, reached: {Reached})", 
                rule.Id, groupValue, totalCount, threshold, thresholdReached);

            return thresholdReached;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating aggregation for rule {RuleId}", rule.Id);
            throw;
        }
    }

    private string ExtractGroupValue(EventEnvelope @event, string? groupByField)
    {
        if (string.IsNullOrEmpty(groupByField))
        {
            return "default";
        }

        // Handle field extraction from Normalized data
        if (@event.Normalized != null)
        {
            // Support dot notation for nested fields (e.g., "Normalized.source_ip")
            if (groupByField.StartsWith("Normalized."))
            {
                var fieldName = groupByField.Substring("Normalized.".Length);
                return ExtractFieldValue(@event.Normalized, fieldName) ?? "unknown";
            }
            
            return ExtractFieldValue(@event.Normalized, groupByField) ?? "unknown";
        }

        return "unknown";
    }

    private string? ExtractFieldValue(object obj, string fieldName)
    {
        var properties = fieldName.Split('.');
        object current = obj;

        foreach (var prop in properties)
        {
            if (current == null) return null;
            
            var property = current.GetType().GetProperty(prop, 
                System.Reflection.BindingFlags.IgnoreCase | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance);
            
            if (property == null) return null;
            
            current = property.GetValue(current);
        }

        return current?.ToString();
    }
}