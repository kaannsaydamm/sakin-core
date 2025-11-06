using System.Text.Json.Serialization;

namespace Sakin.Common.Models;

public record BaselineFeatureSnapshot
{
    [JsonPropertyName("mean")]
    public double Mean { get; init; }
    
    [JsonPropertyName("stddev")]
    public double StdDev { get; init; }
    
    [JsonPropertyName("count")]
    public long Count { get; init; }
    
    [JsonPropertyName("min")]
    public double Min { get; init; }
    
    [JsonPropertyName("max")]
    public double Max { get; init; }
    
    [JsonPropertyName("calculated_at")]
    public DateTime CalculatedAt { get; init; } = DateTime.UtcNow;
}
