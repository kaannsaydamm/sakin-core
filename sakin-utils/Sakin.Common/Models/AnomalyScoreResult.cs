using System.Text.Json.Serialization;

namespace Sakin.Common.Models;

public record AnomalyScoreResult
{
    [JsonPropertyName("score")]
    public double Score { get; init; }
    
    [JsonPropertyName("is_anomalous")]
    public bool IsAnomalous { get; init; }
    
    [JsonPropertyName("z_score")]
    public double ZScore { get; init; }
    
    [JsonPropertyName("reasoning")]
    public string Reasoning { get; init; } = string.Empty;
    
    [JsonPropertyName("baseline_mean")]
    public double? BaselineMean { get; init; }
    
    [JsonPropertyName("baseline_stddev")]
    public double? BaselineStdDev { get; init; }
    
    [JsonPropertyName("current_value")]
    public double? CurrentValue { get; init; }
    
    [JsonPropertyName("metric_name")]
    public string MetricName { get; init; } = string.Empty;
}
