using System.Text.Json;
using Sakin.Common.Models;
using Sakin.Common.Serialization;
using Sakin.Common.Validation;
using Xunit;

namespace Sakin.Common.Tests.Serialization
{
    public class EventEnvelopeSerializerTests
    {
        private readonly EventEnvelopeSerializer _serializer;

        public EventEnvelopeSerializerTests()
        {
            _serializer = new EventEnvelopeSerializer();
        }

        [Fact]
        public void CreateEnvelope_WithBasicEvent_CreatesValidEnvelope()
        {
            // Arrange
            var normalizedEvent = new NetworkEvent
            {
                SourceIp = "192.168.1.100",
                DestinationIp = "8.8.8.8",
                Protocol = Protocol.TCP,
                EventType = EventType.NetworkTraffic,
                Severity = Severity.Info
            };

            // Act
            var envelope = _serializer.CreateEnvelope(
                normalizedEvent,
                "test-sensor",
                SourceType.NetworkSensor,
                new { raw = "data" },
                "1.0.0"
            );

            // Assert
            Assert.NotNull(envelope);
            Assert.Equal("test-sensor", envelope.Source);
            Assert.Equal(SourceType.NetworkSensor, envelope.SourceType);
            Assert.Equal(normalizedEvent, envelope.Normalized);
            Assert.Equal("1.0.0", envelope.SchemaVersion);
            Assert.NotNull(envelope.Raw);
        }

        [Fact]
        public void Serialize_Envelope_ReturnsValidJson()
        {
            // Arrange
            var envelope = _serializer.CreateEnvelope(
                new NetworkEvent
                {
                    SourceIp = "192.168.1.100",
                    DestinationIp = "8.8.8.8",
                    Protocol = Protocol.TCP
                },
                "test-sensor",
                SourceType.NetworkSensor
            );

            // Act
            var json = _serializer.Serialize(envelope);

            // Assert
            Assert.NotNull(json);
            Assert.Contains("test-sensor", json);
            Assert.Contains("networkSensor", json);
            Assert.Contains("normalized", json);
        }

        [Fact]
        public void Deserialize_ValidJson_ReturnsEnvelope()
        {
            // Arrange
            var originalEnvelope = _serializer.CreateEnvelope(
                new NetworkEvent
                {
                    SourceIp = "192.168.1.100",
                    DestinationIp = "8.8.8.8",
                    Protocol = Protocol.TCP,
                    EventType = EventType.NetworkTraffic,
                    Severity = Severity.Info
                },
                "test-sensor",
                SourceType.NetworkSensor
            );
            var json = _serializer.Serialize(originalEnvelope);

            // Act
            var deserializedEnvelope = _serializer.Deserialize(json);

            // Assert
            Assert.NotNull(deserializedEnvelope);
            Assert.Equal(originalEnvelope.Id, deserializedEnvelope.Id);
            Assert.Equal(originalEnvelope.Source, deserializedEnvelope.Source);
            Assert.Equal(originalEnvelope.SourceType, deserializedEnvelope.SourceType);
            Assert.Equal(originalEnvelope.SchemaVersion, deserializedEnvelope.SchemaVersion);
            Assert.Equal(originalEnvelope.Normalized.SourceIp, deserializedEnvelope.Normalized.SourceIp);
            Assert.Equal(originalEnvelope.Normalized.DestinationIp, deserializedEnvelope.Normalized.DestinationIp);
        }

        [Fact]
        public void Deserialize_WithExpectedVersion_ValidatesVersion()
        {
            // Arrange
            var envelope = _serializer.CreateEnvelope(
                new NetworkEvent
                {
                    SourceIp = "192.168.1.100",
                    DestinationIp = "8.8.8.8",
                    Protocol = Protocol.TCP
                },
                "test-sensor",
                SourceType.NetworkSensor,
                schemaVersion: "1.0.0"
            );
            var json = _serializer.Serialize(envelope);

            // Act
            var deserializedEnvelope = _serializer.Deserialize(json, "1.0.0");

            // Assert
            Assert.NotNull(deserializedEnvelope);
            Assert.Equal("1.0.0", deserializedEnvelope.SchemaVersion);
        }

        [Fact]
        public void Deserialize_WithWrongVersion_ThrowsException()
        {
            // Arrange
            var envelope = _serializer.CreateEnvelope(
                new NetworkEvent
                {
                    SourceIp = "192.168.1.100",
                    DestinationIp = "8.8.8.8",
                    Protocol = Protocol.TCP
                },
                "test-sensor",
                SourceType.NetworkSensor,
                schemaVersion: "1.0.0"
            );
            var json = _serializer.Serialize(envelope);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                _serializer.Deserialize(json, "2.0.0"));
        }

        [Fact]
        public void IsVersionCompatible_CompatibleVersions_ReturnsTrue()
        {
            // Act & Assert
            Assert.True(_serializer.IsVersionCompatible("1.0.0", "1.0.0"));
            Assert.True(_serializer.IsVersionCompatible("1.1.0", "1.0.0"));
            Assert.True(_serializer.IsVersionCompatible("1.0.1", "1.0.0"));
            Assert.True(_serializer.IsVersionCompatible("1.2.3", "1.1.0"));
        }

        [Fact]
        public void IsVersionCompatible_IncompatibleVersions_ReturnsFalse()
        {
            // Act & Assert
            Assert.False(_serializer.IsVersionCompatible("2.0.0", "1.0.0"));
            Assert.False(_serializer.IsVersionCompatible("1.0.0", "1.1.0"));
            Assert.False(_serializer.IsVersionCompatible("0.9.0", "1.0.0"));
        }

        [Fact]
        public void GetLatestVersion_ReturnsLatestVersion()
        {
            // Act
            var latestVersion = _serializer.GetLatestVersion();

            // Assert
            Assert.Equal("1.0.0", latestVersion);
        }

        [Fact]
        public void RoundTrip_SerializationDeserialization_NoDataLoss()
        {
            // Arrange
            var originalEnvelope = _serializer.CreateEnvelope(
                new NetworkEvent
                {
                    SourceIp = "192.168.1.100",
                    DestinationIp = "8.8.8.8",
                    SourcePort = 12345,
                    DestinationPort = 443,
                    Protocol = Protocol.HTTPS,
                    EventType = EventType.HttpRequest,
                    Severity = Severity.Info,
                    BytesSent = 1024,
                    BytesReceived = 2048,
                    PacketCount = 5,
                    HttpUrl = "https://example.com/api",
                    HttpMethod = "GET",
                    HttpStatusCode = 200,
                    UserAgent = "test-agent",
                    Metadata = new Dictionary<string, object>
                    {
                        { "test_key", "test_value" },
                        { "number", 42 }
                    },
                    DeviceName = "eth0",
                    SensorId = "sensor-01"
                },
                "test-sensor",
                SourceType.NetworkSensor,
                new
                {
                    rawField1 = "value1",
                    rawField2 = 123,
                    rawField3 = new { nested = "data" }
                },
                "1.0.0"
            );
            originalEnvelope.Enrichment.Add("geoip_country", "US");
            originalEnvelope.Enrichment.Add("asn", 15169);
            originalEnvelope.Metadata.Add("processing_time", 45);

            // Act
            var json = _serializer.Serialize(originalEnvelope);
            var deserializedEnvelope = _serializer.Deserialize(json);

            // Assert
            Assert.NotNull(deserializedEnvelope);
            Assert.Equal(originalEnvelope.Id, deserializedEnvelope.Id);
            Assert.Equal(originalEnvelope.Source, deserializedEnvelope.Source);
            Assert.Equal(originalEnvelope.SourceType, deserializedEnvelope.SourceType);
            Assert.Equal(originalEnvelope.SchemaVersion, deserializedEnvelope.SchemaVersion);
            
            // Check normalized event
            var originalNetwork = (NetworkEvent)originalEnvelope.Normalized;
            var deserializedNetwork = deserializedEnvelope.Normalized as NetworkEvent ?? 
                new NetworkEvent { SourceIp = deserializedEnvelope.Normalized.SourceIp };
            
            Assert.Equal(originalNetwork.SourceIp, deserializedNetwork.SourceIp);
            Assert.Equal(originalNetwork.DestinationIp, deserializedNetwork.DestinationIp);
            Assert.Equal(originalNetwork.SourcePort, deserializedNetwork.SourcePort);
            Assert.Equal(originalNetwork.DestinationPort, deserializedNetwork.DestinationPort);
            Assert.Equal(originalNetwork.Protocol, deserializedNetwork.Protocol);
            Assert.Equal(originalNetwork.EventType, deserializedNetwork.EventType);
            Assert.Equal(originalNetwork.Severity, deserializedNetwork.Severity);
            Assert.Equal(originalNetwork.BytesSent, deserializedNetwork.BytesSent);
            Assert.Equal(originalNetwork.BytesReceived, deserializedNetwork.BytesReceived);
            Assert.Equal(originalNetwork.PacketCount, deserializedNetwork.PacketCount);
            Assert.Equal(originalNetwork.HttpUrl, deserializedNetwork.HttpUrl);
            Assert.Equal(originalNetwork.HttpMethod, deserializedNetwork.HttpMethod);
            Assert.Equal(originalNetwork.HttpStatusCode, deserializedNetwork.HttpStatusCode);
            Assert.Equal(originalNetwork.UserAgent, deserializedNetwork.UserAgent);
        }

        [Fact]
        public void RegisterVersion_AddsNewVersion()
        {
            // Arrange
            var customOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            // Act
            _serializer.RegisterVersion("2.0.0", customOptions);
            var latestVersion = _serializer.GetLatestVersion();

            // Assert
            Assert.Equal("2.0.0", latestVersion);
        }
    }
}