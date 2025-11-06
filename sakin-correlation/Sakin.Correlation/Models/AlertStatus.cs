using System.Text.Json.Serialization;

namespace Sakin.Correlation.Models;

public enum AlertStatus
{
    [JsonPropertyName("new")]
    New,
    
    [JsonPropertyName("pending_score")]
    PendingScore,
    
    [JsonPropertyName("acknowledged")]
    Acknowledged,
    
    [JsonPropertyName("under_investigation")]
    UnderInvestigation,
    
    [JsonPropertyName("resolved")]
    Resolved,
    
    [JsonPropertyName("closed")]
    Closed,
    
    [JsonPropertyName("false_positive")]
    FalsePositive
}
