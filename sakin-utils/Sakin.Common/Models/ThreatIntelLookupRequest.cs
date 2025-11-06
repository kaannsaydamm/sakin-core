using System.Text.Json.Serialization;

namespace Sakin.Common.Models
{
    public record ThreatIntelLookupRequest
    {
        [JsonPropertyName("type")]
        public ThreatIntelIndicatorType Type { get; init; }

        [JsonPropertyName("value")]
        public string Value { get; init; } = string.Empty;

        [JsonPropertyName("hash_type")]
        public ThreatIntelHashType? HashType { get; init; }
    }
}
