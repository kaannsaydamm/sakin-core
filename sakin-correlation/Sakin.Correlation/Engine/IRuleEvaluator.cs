using Sakin.Correlation.Models;
using Sakin.Common.Models;

namespace Sakin.Correlation.Engine;

public interface IRuleEvaluator
{
    Task<EvaluationResult> EvaluateAsync(CorrelationRule rule, EventEnvelope eventEnvelope);
    Task<EvaluationResult> EvaluateWithAggregationAsync(CorrelationRule rule, IEnumerable<EventEnvelope> events);
}

public record EvaluationResult
{
    public bool IsMatch { get; init; }
    public bool ShouldTriggerAlert { get; init; }
    public string? Reason { get; init; }
    public Dictionary<string, object> Context { get; init; } = new();
    public int? AggregationCount { get; init; }
    public double? AggregatedValue { get; init; }
    public List<string> MatchedConditions { get; init; } = new();
}
