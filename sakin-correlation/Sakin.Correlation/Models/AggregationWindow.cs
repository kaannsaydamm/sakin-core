using System.Text.Json.Serialization;

namespace Sakin.Correlation.Models;

public class AggregationWindow
{
    [JsonPropertyName("type")]
    public AggregationType Type { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("unit")]
    public TimeUnit Unit { get; set; }

    [JsonPropertyName("groupBy")]
    public List<string>? GroupBy { get; set; }

    [JsonPropertyName("having")]
    public Condition? Having { get; set; }
}

public enum AggregationType
{
    [JsonPropertyName("time_window")]
    TimeWindow,
    
    [JsonPropertyName("count")]
    Count,
    
    [JsonPropertyName("sum")]
    Sum,
    
    [JsonPropertyName("average")]
    Average,
    
    [JsonPropertyName("min")]
    Min,
    
    [JsonPropertyName("max")]
    Max
}

public enum TimeUnit
{
    [JsonPropertyName("seconds")]
    Seconds,
    
    [JsonPropertyName("minutes")]
    Minutes,
    
    [JsonPropertyName("hours")]
    Hours,
    
    [JsonPropertyName("days")]
    Days
}