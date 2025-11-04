using System.Text.Json.Serialization;

namespace Sakin.Correlation.Models;

public enum SeverityLevel
{
    [JsonPropertyName("low")]
    Low,
    
    [JsonPropertyName("medium")]
    Medium,
    
    [JsonPropertyName("high")]
    High,
    
    [JsonPropertyName("critical")]
    Critical
}