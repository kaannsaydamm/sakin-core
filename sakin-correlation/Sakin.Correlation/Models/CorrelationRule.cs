using System.Text.Json.Serialization;

namespace Sakin.Correlation.Models;

public class CorrelationRule
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("severity")]
    public SeverityLevel Severity { get; set; }

    [JsonPropertyName("triggers")]
    public List<Trigger> Triggers { get; set; } = new();

    [JsonPropertyName("conditions")]
    public List<Condition> Conditions { get; set; } = new();

    [JsonPropertyName("aggregation")]
    public AggregationWindow? Aggregation { get; set; }

    [JsonPropertyName("actions")]
    public List<Action> Actions { get; set; } = new();

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}