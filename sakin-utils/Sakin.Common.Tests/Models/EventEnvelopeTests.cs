using Sakin.Common.Models;
using Sakin.Common.Validation;
using Xunit;

namespace Sakin.Common.Tests.Models
{
    public class EventEnvelopeTests
    {
        private readonly EventValidator _validator;

        public EventEnvelopeTests()
        {
            _validator = EventValidator.FromFile("/home/engine/project/schema/event-schema.json");
        }

        [Fact]
        public void EventEnvelope_WithRequiredFields_IsValid()
        {
            // Arrange
            var envelope = new EventEnvelope
            {
                Source = "test-sensor",
                SourceType = SourceType.NetworkSensor,
                Normalized = new NetworkEvent
                {
                    SourceIp = "192.168.1.100",
                    DestinationIp = "8.8.8.8",
                    Protocol = Protocol.TCP
                },
                SchemaVersion = "1.0.0"
            };

            // Act
            var result = _validator.ValidateEnvelope(envelope);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void EventEnvelope_WithAllFields_IsValid()
        {
            // Arrange
            var envelope = new EventEnvelope
            {
                Source = "test-sensor",
                SourceType = SourceType.NetworkSensor,
                Raw = new { packet = "data" },
                Normalized = new NetworkEvent
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
                        { "test_key", "test_value" }
                    },
                    DeviceName = "eth0",
                    SensorId = "sensor-01"
                },
                Enrichment = new Dictionary<string, object>
                {
                    { "geoip_country", "US" },
                    { "asn", 15169 }
                },
                SchemaVersion = "1.0.0",
                Metadata = new Dictionary<string, object>
                {
                    { "processing_time", 45 }
                }
            };

            // Act
            var result = _validator.ValidateEnvelope(envelope);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void EventEnvelope_WithNormalizedEvent_IsValid()
        {
            // Arrange
            var envelope = new EventEnvelope
            {
                Source = "log-collector",
                SourceType = SourceType.LogCollector,
                Normalized = new NormalizedEvent
                {
                    SourceIp = "192.168.1.100",
                    DestinationIp = "192.168.1.1",
                    Protocol = Protocol.Unknown,
                    EventType = EventType.SystemLog,
                    Severity = Severity.Low,
                    Payload = "System log message"
                },
                SchemaVersion = "1.0.0"
            };

            // Act
            var result = _validator.ValidateEnvelope(envelope);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void EventEnvelope_Serialization_RoundTrip()
        {
            // Arrange
            var originalEnvelope = new EventEnvelope
            {
                Source = "test-sensor",
                SourceType = SourceType.NetworkSensor,
                Raw = new { packet = "data" },
                Normalized = new NetworkEvent
                {
                    SourceIp = "192.168.1.100",
                    DestinationIp = "8.8.8.8",
                    Protocol = Protocol.TCP,
                    EventType = EventType.NetworkTraffic,
                    Severity = Severity.Info,
                    BytesSent = 1024,
                    BytesReceived = 2048,
                    PacketCount = 5
                },
                Enrichment = new Dictionary<string, object>
                {
                    { "geoip_country", "US" },
                    { "asn", 15169 }
                },
                SchemaVersion = "1.0.0",
                Metadata = new Dictionary<string, object>
                {
                    { "processing_time", 45 }
                }
            };

            // Act
            var json = _validator.SerializeEnvelope(originalEnvelope);
            var deserializedEnvelope = _validator.DeserializeEnvelope(json);

            // Assert
            Assert.NotNull(deserializedEnvelope);
            Assert.Equal(originalEnvelope.Source, deserializedEnvelope.Source);
            Assert.Equal(originalEnvelope.SourceType, deserializedEnvelope.SourceType);
            Assert.Equal(originalEnvelope.SchemaVersion, deserializedEnvelope.SchemaVersion);
            
            // Check that base normalized properties are preserved
            Assert.Equal(originalEnvelope.Normalized.SourceIp, deserializedEnvelope.Normalized.SourceIp);
            Assert.Equal(originalEnvelope.Normalized.DestinationIp, deserializedEnvelope.Normalized.DestinationIp);
            Assert.Equal(originalEnvelope.Normalized.Protocol, deserializedEnvelope.Normalized.Protocol);
            Assert.Equal(originalEnvelope.Normalized.EventType, deserializedEnvelope.Normalized.EventType);
            Assert.Equal(originalEnvelope.Normalized.Severity, deserializedEnvelope.Normalized.Severity);
        }

        [Theory]
        [InlineData("1.0.0", true)]
        [InlineData("2.1.3", true)]
        [InlineData("10.5.0", true)]
        [InlineData("1.0", false)]
        [InlineData("v1.0.0", false)]
        [InlineData("1.0.0-beta", false)]
        [InlineData("invalid", false)]
        public void EventEnvelope_SchemaVersionValidation(string version, bool shouldBeValid)
        {
            // Arrange
            var envelope = new EventEnvelope
            {
                Source = "test-sensor",
                SourceType = SourceType.NetworkSensor,
                Normalized = new NetworkEvent
                {
                    SourceIp = "192.168.1.100",
                    DestinationIp = "8.8.8.8",
                    Protocol = Protocol.TCP
                },
                SchemaVersion = version
            };

            // Act
            var result = _validator.ValidateEnvelope(envelope);

            // Assert
            if (shouldBeValid)
            {
                Assert.True(result.IsValid, $"Version {version} should be valid");
            }
            else
            {
                Assert.False(result.IsValid, $"Version {version} should be invalid");
            }
        }

        [Fact]
        public void EventEnvelope_WithDifferentSourceTypes_IsValid()
        {
            // Arrange
            var sourceTypes = new[]
            {
                SourceType.Unknown,
                SourceType.NetworkSensor,
                SourceType.LogCollector,
                SourceType.ApiGateway,
                SourceType.SecurityAgent,
                SourceType.FileMonitor,
                SourceType.ProcessMonitor,
                SourceType.DnsCollector,
                SourceType.WebProxy,
                SourceType.Firewall,
                SourceType.IntrusionDetection,
                SourceType.EndpointProtection
            };

            foreach (var sourceType in sourceTypes)
            {
                var envelope = new EventEnvelope
                {
                    Source = "test-source",
                    SourceType = sourceType,
                    Normalized = new NetworkEvent
                    {
                        SourceIp = "192.168.1.100",
                        DestinationIp = "8.8.8.8",
                        Protocol = Protocol.TCP
                    },
                    SchemaVersion = "1.0.0"
                };

                // Act
                var result = _validator.ValidateEnvelope(envelope);

                // Assert
                Assert.True(result.IsValid, $"Source type {sourceType} should be valid");
            }
        }
    }
}