using System.Text.Json.Serialization;

namespace Sakin.Common.Models
{
    public record NormalizedEvent
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; } = Guid.NewGuid();
        
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        
        [JsonPropertyName("eventType")]
        public EventType EventType { get; init; } = EventType.Unknown;
        
        [JsonPropertyName("severity")]
        public Severity Severity { get; init; } = Severity.Info;
        
        [JsonPropertyName("sourceIp")]
        public string SourceIp { get; init; } = string.Empty;
        
        [JsonPropertyName("destinationIp")]
        public string DestinationIp { get; init; } = string.Empty;
        
        [JsonPropertyName("sourcePort")]
        public int? SourcePort { get; init; }
        
        [JsonPropertyName("destinationPort")]
        public int? DestinationPort { get; init; }
        
        [JsonPropertyName("protocol")]
        public Protocol Protocol { get; init; } = Protocol.Unknown;
        
        [JsonPropertyName("payload")]
        public string? Payload { get; init; }
        
        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; init; } = new();
        
        [JsonPropertyName("deviceName")]
        public string? DeviceName { get; init; }
        
        [JsonPropertyName("sensorId")]
        public string? SensorId { get; init; }
    }
}
