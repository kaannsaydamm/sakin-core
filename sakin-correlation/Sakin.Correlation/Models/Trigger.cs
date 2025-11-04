using System.Text.Json.Serialization;

namespace Sakin.Correlation.Models;

public class Trigger
{
    [JsonPropertyName("type")]
    public TriggerType Type { get; set; }

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("filters")]
    public Dictionary<string, object>? Filters { get; set; }
}

public enum TriggerType
{
    [JsonPropertyName("event")]
    Event,
    
    [JsonPropertyName("time")]
    Time,
    
    [JsonPropertyName("threshold")]
    Threshold
}