using System.Text.Json.Serialization;

namespace Sakin.Correlation.Models;

public class AggregationCondition
{
    [JsonPropertyName("function")]
    public string Function { get; set; } = "count";

    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("group_by")]
    public string? GroupBy { get; set; }

    [JsonPropertyName("window_seconds")]
    public int WindowSeconds { get; set; }
}

public class ConditionWithAggregation
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "equals";

    [JsonPropertyName("value")]
    public object? Value { get; set; }

    [JsonPropertyName("aggregation")]
    public AggregationCondition? Aggregation { get; set; }
}

public class RuleTrigger
{
    [JsonPropertyName("source_types")]
    public List<string> SourceTypes { get; set; } = new();

    [JsonPropertyName("match")]
    public Dictionary<string, object> Match { get; set; } = new();
}