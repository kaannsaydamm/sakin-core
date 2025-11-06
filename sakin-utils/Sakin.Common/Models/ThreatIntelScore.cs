using System.Text.Json.Serialization;

namespace Sakin.Common.Models
{
    public record ThreatIntelScore
    {
        [JsonPropertyName("is_malicious")]
        public bool IsKnownMalicious { get; init; }

        [JsonPropertyName("score")]
        public int Score { get; init; }

        [JsonPropertyName("feeds")]
        public string[] MatchingFeeds { get; init; } = Array.Empty<string>();

        [JsonPropertyName("last_seen")]
        public DateTimeOffset? LastSeen { get; init; }

        [JsonPropertyName("details")]
        public Dictionary<string, object> Details { get; init; } = new();
    }
}
