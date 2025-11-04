using Sakin.Common.Models;
using Sakin.Common.Serialization;
using System.Text.Json;
using Xunit;

namespace Sakin.Common.Tests.Models
{
    public class EventEnvelopeTests
    {
        [Fact]
        public void EventEnvelope_InitializesWithDefaults()
        {
            var envelope = new EventEnvelope();

            Assert.NotEqual(Guid.Empty, envelope.EventId);
            Assert.True(envelope.ReceivedAt <= DateTimeOffset.UtcNow);
            Assert.Equal(string.Empty, envelope.Source);
            Assert.Equal(string.Empty, envelope.SourceType);
            Assert.Equal(string.Empty, envelope.Raw);
            Assert.Null(envelope.Normalized);
            Assert.NotNull(envelope.Enrichment);
            Assert.Empty(envelope.Enrichment);
            Assert.Equal("v1.0", envelope.SchemaVersion);
        }

        [Fact]
        public void EventEnvelope_CanSetProperties()
        {
            var normalizedEvent = new NormalizedEvent
            {
                EventType = EventType.NetworkTraffic,
                Severity = Severity.Medium,
                SourceIp = "192.168.1.100",
                DestinationIp = "203.0.113.1",
                Protocol = Protocol.TCP
            };

            var envelope = new EventEnvelope
            {
                Source = "sensor-01",
                SourceType = "network-sensor",
                Raw = "{\"test\": \"data\"}",
                Normalized = normalizedEvent,
                SchemaVersion = "v1.1"
            };

            Assert.Equal("sensor-01", envelope.Source);
            Assert.Equal("network-sensor", envelope.SourceType);
            Assert.Equal("{\"test\": \"data\"}", envelope.Raw);
            Assert.Equal(normalizedEvent, envelope.Normalized);
            Assert.Equal("v1.1", envelope.SchemaVersion);
        }

        [Fact]
        public void EventEnvelope_CanAddEnrichment()
        {
            var envelope = new EventEnvelope
            {
                Enrichment = new Dictionary<string, object>
                {
                    ["geoLocation"] = new { country = "US", city = "New York" },
                    ["riskScore"] = 85
                }
            };

            Assert.Equal(2, envelope.Enrichment.Count);
            Assert.True(envelope.Enrichment.ContainsKey("geoLocation"));
            Assert.True(envelope.Enrichment.ContainsKey("riskScore"));
        }

        [Fact]
        public void EventEnvelope_SerializesToCamelCase()
        {
            var envelope = new EventEnvelope
            {
                Source = "sensor-01",
                SourceType = "network-sensor",
                Normalized = new NormalizedEvent
                {
                    EventType = EventType.NetworkTraffic,
                    Severity = Severity.Info
                }
            };

            var json = EventEnvelopeSerializer.Serialize(envelope);
            var document = JsonDocument.Parse(json);

            Assert.True(document.RootElement.TryGetProperty("eventId", out _));
            Assert.True(document.RootElement.TryGetProperty("receivedAt", out _));
            Assert.True(document.RootElement.TryGetProperty("source", out _));
            Assert.True(document.RootElement.TryGetProperty("sourceType", out _));
            Assert.True(document.RootElement.TryGetProperty("normalized", out _));
            Assert.True(document.RootElement.TryGetProperty("schemaVersion", out _));
        }

        [Fact]
        public void EventEnvelope_RoundTripSerialization()
        {
            var originalEnvelope = new EventEnvelope
            {
                Source = "sensor-01",
                SourceType = "network-sensor",
                Raw = "{\"timestamp\":\"2024-01-15T10:30:00Z\",\"srcIp\":\"192.168.1.100\"}",
                Normalized = new NormalizedEvent
                {
                    EventType = EventType.NetworkTraffic,
                    Severity = Severity.Medium,
                    SourceIp = "192.168.1.100",
                    DestinationIp = "203.0.113.1",
                    Protocol = Protocol.TCP,
                    Metadata = new Dictionary<string, object>
                    {
                        ["interface"] = "eth0"
                    }
                },
                Enrichment = new Dictionary<string, object>
                {
                    ["geoLocation"] = new { country = "US" },
                    ["riskScore"] = 75
                }
            };

            var json = EventEnvelopeSerializer.Serialize(originalEnvelope);
            var deserializedEnvelope = EventEnvelopeSerializer.Deserialize(json);

            Assert.Equal(originalEnvelope.EventId, deserializedEnvelope.EventId);
            Assert.Equal(originalEnvelope.Source, deserializedEnvelope.Source);
            Assert.Equal(originalEnvelope.SourceType, deserializedEnvelope.SourceType);
            Assert.Equal(originalEnvelope.Raw, deserializedEnvelope.Raw);
            Assert.Equal(originalEnvelope.SchemaVersion, deserializedEnvelope.SchemaVersion);
            Assert.NotNull(deserializedEnvelope.Normalized);
            Assert.Equal(originalEnvelope.Normalized!.EventType, deserializedEnvelope.Normalized.EventType);
            Assert.Equal(originalEnvelope.Normalized.SourceIp, deserializedEnvelope.Normalized.SourceIp);
            Assert.Equal(originalEnvelope.Enrichment.Count, deserializedEnvelope.Enrichment.Count);
        }
    }
}