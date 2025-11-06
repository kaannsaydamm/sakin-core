using System.Text.Json.Serialization;

namespace Sakin.Correlation.Models;

public class CorrelationRuleV2
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("trigger")]
    public RuleTrigger Trigger { get; set; } = new();

    [JsonPropertyName("condition")]
    public ConditionWithAggregation Condition { get; set; } = new();

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "medium";

    [JsonPropertyName("actions")]
    public List<object> Actions { get; set; } = new();

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}