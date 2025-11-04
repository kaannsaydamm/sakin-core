using System.Text.Json.Serialization;

namespace Sakin.Common.Models
{
    public record NetworkEvent : NormalizedEvent
    {
        public NetworkEvent()
        {
            EventType = EventType.NetworkTraffic;
        }

        [JsonPropertyName("bytesSent")]
        public long BytesSent { get; init; }
        
        [JsonPropertyName("bytesReceived")]
        public long BytesReceived { get; init; }
        
        [JsonPropertyName("packetCount")]
        public int PacketCount { get; init; }
        
        [JsonPropertyName("sni")]
        public string? Sni { get; init; }
        
        [JsonPropertyName("httpUrl")]
        public string? HttpUrl { get; init; }
        
        [JsonPropertyName("httpMethod")]
        public string? HttpMethod { get; init; }
        
        [JsonPropertyName("httpStatusCode")]
        public int? HttpStatusCode { get; init; }
        
        [JsonPropertyName("userAgent")]
        public string? UserAgent { get; init; }
    }
}
