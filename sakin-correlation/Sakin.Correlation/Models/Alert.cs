using System.Text.Json.Serialization;
using Sakin.Common.Models;

namespace Sakin.Correlation.Models;

public record Alert
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("ruleName")]
    public string RuleName { get; init; } = string.Empty;

    [JsonPropertyName("ruleId")]
    public string RuleId { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public Severity Severity { get; init; } = Severity.Info;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("eventIds")]
    public List<Guid> EventIds { get; init; } = new();

    [JsonPropertyName("eventCount")]
    public int EventCount { get; init; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; init; } = new();

    [JsonPropertyName("sourceIp")]
    public string? SourceIp { get; init; }

    [JsonPropertyName("destinationIp")]
    public string? DestinationIp { get; init; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; init; } = new();
}
