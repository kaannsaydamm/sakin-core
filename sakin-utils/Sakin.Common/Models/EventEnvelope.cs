using System.Text.Json.Serialization;

namespace Sakin.Common.Models
{
    public record EventEnvelope
    {
        [JsonPropertyName("eventId")]
        public Guid EventId { get; init; } = Guid.NewGuid();
        
        [JsonPropertyName("receivedAt")]
        public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
        
        [JsonPropertyName("source")]
        public string Source { get; init; } = string.Empty;
        
        [JsonPropertyName("sourceType")]
        public string SourceType { get; init; } = string.Empty;
        
        [JsonPropertyName("raw")]
        public string Raw { get; init; } = string.Empty;
        
        [JsonPropertyName("normalized")]
        public NormalizedEvent? Normalized { get; init; }
        
        [JsonPropertyName("enrichment")]
        public Dictionary<string, object> Enrichment { get; init; } = new();
        
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "v1.0";
    }
}