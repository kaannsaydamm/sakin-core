using System.Text.Json.Serialization;

namespace Sakin.Correlation.Models;

public enum AlertStatus
{
    [JsonPropertyName("new")]
    New,
    
    [JsonPropertyName("acknowledged")]
    Acknowledged,
    
    [JsonPropertyName("resolved")]
    Resolved
}
