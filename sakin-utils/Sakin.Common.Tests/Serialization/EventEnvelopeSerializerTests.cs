using Sakin.Common.Models;
using Sakin.Common.Serialization;
using System.Text.Json;
using Xunit;

namespace Sakin.Common.Tests.Serialization
{
    public class EventEnvelopeSerializerTests
    {
        [Fact]
        public void Serialize_EventEnvelope_ReturnsValidJson()
        {
            var envelope = new EventEnvelope
            {
                Source = "sensor-01",
                SourceType = "network-sensor",
                Normalized = new NormalizedEvent
                {
                    EventType = EventType.NetworkTraffic,
                    Severity = Severity.Info,
                    SourceIp = "192.168.1.100",
                    DestinationIp = "203.0.113.1",
                    Protocol = Protocol.TCP
                }
            };

            var json = EventEnvelopeSerializer.Serialize(envelope);

            Assert.NotNull(json);
            Assert.NotEmpty(json);
            
            var document = JsonDocument.Parse(json);
            Assert.True(document.RootElement.TryGetProperty("eventId", out _));
            Assert.Equal("sensor-01", document.RootElement.GetProperty("source").GetString());
            Assert.Equal("network-sensor", document.RootElement.GetProperty("sourceType").GetString());
        }

        [Fact]
        public void Deserialize_ValidJson_ReturnsEventEnvelope()
        {
            var json = @"{
                ""eventId"": ""550e8400-e29b-41d4-a716-446655440000"",
                ""receivedAt"": ""2024-01-15T10:30:00Z"",
                ""source"": ""sensor-01"",
                ""sourceType"": ""network-sensor"",
                ""raw"": ""{\""test\"": \""data\""}"",
                ""normalized"": {
                    ""id"": ""550e8400-e29b-41d4-a716-446655440001"",
                    ""timestamp"": ""2024-01-15T10:30:00Z"",
                    ""eventType"": ""networkTraffic"",
                    ""severity"": ""info"",
                    ""sourceIp"": ""192.168.1.100"",
                    ""destinationIp"": ""203.0.113.1"",
                    ""protocol"": ""tcp""
                },
                ""enrichment"": {
                    ""geoLocation"": {""country"": ""US""}
                },
                ""schemaVersion"": ""v1.0""
            }";

            var envelope = EventEnvelopeSerializer.Deserialize(json);

            Assert.Equal(Guid.Parse("550e8400-e29b-41d4-a716-446655440000"), envelope.EventId);
            Assert.Equal("sensor-01", envelope.Source);
            Assert.Equal("network-sensor", envelope.SourceType);
            Assert.Equal("{\"test\": \"data\"}", envelope.Raw);
            Assert.NotNull(envelope.Normalized);
            Assert.Equal(EventType.NetworkTraffic, envelope.Normalized.EventType);
            Assert.Equal("192.168.1.100", envelope.Normalized.SourceIp);
            Assert.Equal("v1.0", envelope.SchemaVersion);
        }

        [Fact]
        public void Deserialize_InvalidJson_ThrowsJsonException()
        {
            var invalidJson = "{ invalid json }";

            Assert.Throws<JsonException>(() => EventEnvelopeSerializer.Deserialize(invalidJson));
        }

        [Fact]
        public void TryDeserialize_ValidJson_ReturnsTrue()
        {
            var json = @"{
                ""eventId"": ""550e8400-e29b-41d4-a716-446655440000"",
                ""source"": ""sensor-01"",
                ""sourceType"": ""network-sensor"",
                ""schemaVersion"": ""v1.0""
            }";

            var result = EventEnvelopeSerializer.TryDeserialize(json, out var envelope);

            Assert.True(result);
            Assert.NotNull(envelope);
            Assert.Equal("sensor-01", envelope.Source);
        }

        [Fact]
        public void TryDeserialize_InvalidJson_ReturnsFalse()
        {
            var invalidJson = "{ invalid json }";

            var result = EventEnvelopeSerializer.TryDeserialize(invalidJson, out var envelope);

            Assert.False(result);
            Assert.Null(envelope);
        }

        [Fact]
        public void SerializeNormalized_NormalizedEvent_ReturnsValidJson()
        {
            var normalizedEvent = new NormalizedEvent
            {
                EventType = EventType.DnsQuery,
                Severity = Severity.Low,
                SourceIp = "192.168.1.100",
                DestinationIp = "8.8.8.8",
                Protocol = Protocol.UDP
            };

            var json = EventEnvelopeSerializer.SerializeNormalized(normalizedEvent);

            Assert.NotNull(json);
            Assert.NotEmpty(json);
            
            var document = JsonDocument.Parse(json);
            Assert.Equal("dnsQuery", document.RootElement.GetProperty("eventType").GetString());
            Assert.Equal("low", document.RootElement.GetProperty("severity").GetString());
            Assert.Equal("udp", document.RootElement.GetProperty("protocol").GetString());
        }

        [Fact]
        public void DeserializeNormalized_NetworkEvent_ReturnsNetworkEvent()
        {
            var json = @"{
                ""id"": ""550e8400-e29b-41d4-a716-446655440001"",
                ""timestamp"": ""2024-01-15T10:30:00Z"",
                ""eventType"": ""networkTraffic"",
                ""severity"": ""info"",
                ""sourceIp"": ""192.168.1.100"",
                ""destinationIp"": ""203.0.113.1"",
                ""protocol"": ""tcp"",
                ""bytesSent"": 1024,
                ""bytesReceived"": 2048,
                ""packetCount"": 10
            }";

            var networkEvent = EventEnvelopeSerializer.DeserializeNormalized<NetworkEvent>(json);

            Assert.NotNull(networkEvent);
            Assert.Equal(EventType.NetworkTraffic, networkEvent.EventType);
            Assert.Equal(1024, networkEvent.BytesSent);
            Assert.Equal(2048, networkEvent.BytesReceived);
            Assert.Equal(10, networkEvent.PacketCount);
        }

        [Fact]
        public void TryDeserializeNormalized_ValidJson_ReturnsTrue()
        {
            var json = @"{
                ""id"": ""550e8400-e29b-41d4-a716-446655440001"",
                ""timestamp"": ""2024-01-15T10:30:00Z"",
                ""eventType"": ""networkTraffic"",
                ""severity"": ""info"",
                ""sourceIp"": ""192.168.1.100"",
                ""destinationIp"": ""203.0.113.1"",
                ""protocol"": ""tcp""
            }";

            var result = EventEnvelopeSerializer.TryDeserializeNormalized<NetworkEvent>(json, out var networkEvent);

            Assert.True(result);
            Assert.NotNull(networkEvent);
            Assert.Equal(EventType.NetworkTraffic, networkEvent.EventType);
        }

        [Fact]
        public void VersionCompatibility_DeserializesV1_0()
        {
            var json = @"{
                ""eventId"": ""550e8400-e29b-41d4-a716-446655440000"",
                ""receivedAt"": ""2024-01-15T10:30:00Z"",
                ""source"": ""sensor-01"",
                ""sourceType"": ""network-sensor"",
                ""schemaVersion"": ""v1.0""
            }";

            var envelope = EventEnvelopeSerializer.Deserialize(json);

            Assert.Equal("v1.0", envelope.SchemaVersion);
        }

        [Fact]
        public void VersionCompatibility_DeserializesV1_1()
        {
            var json = @"{
                ""eventId"": ""550e8400-e29b-41d4-a716-446655440000"",
                ""receivedAt"": ""2024-01-15T10:30:00Z"",
                ""source"": ""sensor-01"",
                ""sourceType"": ""network-sensor"",
                ""schemaVersion"": ""v1.1""
            }";

            var envelope = EventEnvelopeSerializer.Deserialize(json);

            Assert.Equal("v1.1", envelope.SchemaVersion);
        }
    }
}