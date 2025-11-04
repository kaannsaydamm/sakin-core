using System.Text.Json.Serialization;

namespace Sakin.Common.Models
{
    /// <summary>
    /// Defines the type of event source
    /// </summary>
    public enum SourceType
    {
        Unknown = 0,
        NetworkSensor = 1,
        LogCollector = 2,
        ApiGateway = 3,
        SecurityAgent = 4,
        FileMonitor = 5,
        ProcessMonitor = 6,
        DnsCollector = 7,
        WebProxy = 8,
        Firewall = 9,
        IntrusionDetection = 10,
        EndpointProtection = 11
    }

    /// <summary>
    /// Envelope structure for all events containing metadata, raw data, and normalized events
    /// </summary>
    public record EventEnvelope
    {
        /// <summary>
        /// Unique identifier for the envelope
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Timestamp when the event was received by the system (ISO 8601 format)
        /// </summary>
        [JsonPropertyName("receivedAt")]
        public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Source identifier that generated the event
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; init; } = string.Empty;

        /// <summary>
        /// Type of source that generated the event
        /// </summary>
        [JsonPropertyName("sourceType")]
        public SourceType SourceType { get; init; } = SourceType.Unknown;

        /// <summary>
        /// Raw event data as received from the source
        /// </summary>
        [JsonPropertyName("raw")]
        public object? Raw { get; init; }

        /// <summary>
        /// Normalized event data conforming to the event schema
        /// </summary>
        [JsonPropertyName("normalized")]
        public NormalizedEvent Normalized { get; init; } = new NormalizedEvent();

        /// <summary>
        /// Enrichment data added during processing (GeoIP, threat intel, etc.)
        /// </summary>
        [JsonPropertyName("enrichment")]
        public Dictionary<string, object> Enrichment { get; init; } = new();

        /// <summary>
        /// Schema version for the normalized event data
        /// </summary>
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "1.0.0";

        /// <summary>
        /// Additional metadata about the envelope or processing
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; init; } = new();
    }
}