using Sakin.Common.Models;
using Sakin.Common.Validation;
using Xunit;

namespace Sakin.Common.Tests.Validation
{
    public class EventValidatorTests
    {
        private readonly EventValidator _validator;
        private readonly string _schemaPath;

        public EventValidatorTests()
        {
            _schemaPath = Path.Combine(GetProjectRoot(), "schema", "event-schema.json");
            _validator = EventValidator.FromFile(_schemaPath);
        }

        private static string GetProjectRoot()
        {
            var directory = Directory.GetCurrentDirectory();
            while (directory != null && !File.Exists(Path.Combine(directory, "SAKINCore-CS.sln")))
            {
                directory = Directory.GetParent(directory)?.FullName;
            }
            return directory ?? throw new InvalidOperationException("Could not find project root");
        }

        [Fact]
        public void Validate_ValidNormalizedEvent_ReturnsValid()
        {
            var evt = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = EventType.NetworkTraffic,
                Severity = Severity.Info,
                SourceIp = "192.168.1.100",
                DestinationIp = "8.8.8.8",
                SourcePort = 54321,
                DestinationPort = 443,
                Protocol = Protocol.HTTPS,
                DeviceName = "eth0",
                SensorId = "sensor-01"
            };

            var result = _validator.Validate(evt);

            Assert.True(result.IsValid, string.Join(", ", result.Errors));
        }

        [Fact]
        public void Validate_ValidNetworkEvent_ReturnsValid()
        {
            var evt = new NetworkEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Severity = Severity.Info,
                SourceIp = "192.168.1.100",
                DestinationIp = "93.184.216.34",
                SourcePort = 54322,
                DestinationPort = 443,
                Protocol = Protocol.HTTPS,
                BytesSent = 1024,
                BytesReceived = 4096,
                PacketCount = 15,
                Sni = "example.com",
                HttpUrl = "https://example.com/api/data",
                HttpMethod = "GET",
                HttpStatusCode = 200,
                UserAgent = "Mozilla/5.0",
                DeviceName = "eth0",
                SensorId = "sensor-01"
            };

            var result = _validator.Validate(evt);

            Assert.True(result.IsValid, string.Join(", ", result.Errors));
        }

        [Fact]
        public void Validate_NetworkEventWithMetadata_ReturnsValid()
        {
            var evt = new NetworkEvent
            {
                SourceIp = "192.168.1.100",
                DestinationIp = "8.8.8.8",
                Protocol = Protocol.TCP,
                Metadata = new Dictionary<string, object>
                {
                    { "geoip_country", "US" },
                    { "geoip_city", "New York" },
                    { "asn", 15169 },
                    { "threat_score", 85.5 }
                }
            };

            var result = _validator.Validate(evt);

            Assert.True(result.IsValid, string.Join(", ", result.Errors));
        }

        [Fact]
        public void Validate_EventWithNullOptionalFields_ReturnsValid()
        {
            var evt = new NormalizedEvent
            {
                SourceIp = "192.168.1.100",
                DestinationIp = "8.8.8.8",
                Protocol = Protocol.TCP,
                SourcePort = null,
                DestinationPort = null,
                Payload = null,
                DeviceName = null,
                SensorId = null
            };

            var result = _validator.Validate(evt);

            Assert.True(result.IsValid, string.Join(", ", result.Errors));
        }

        [Fact]
        public void Validate_EventWithEmptyMetadata_ReturnsValid()
        {
            var evt = new NormalizedEvent
            {
                SourceIp = "192.168.1.100",
                DestinationIp = "8.8.8.8",
                Protocol = Protocol.TCP,
                Metadata = new Dictionary<string, object>()
            };

            var result = _validator.Validate(evt);

            Assert.True(result.IsValid, string.Join(", ", result.Errors));
        }

        [Fact]
        public void Validate_EventWithAllEventTypes_ReturnsValid()
        {
            foreach (EventType eventType in Enum.GetValues(typeof(EventType)))
            {
                var evt = new NormalizedEvent
                {
                    EventType = eventType,
                    SourceIp = "192.168.1.100",
                    DestinationIp = "8.8.8.8",
                    Protocol = Protocol.TCP
                };

                var result = _validator.Validate(evt);

                Assert.True(result.IsValid, $"EventType {eventType} failed: {string.Join(", ", result.Errors)}");
            }
        }

        [Fact]
        public void Validate_EventWithAllSeverities_ReturnsValid()
        {
            foreach (Severity severity in Enum.GetValues(typeof(Severity)))
            {
                var evt = new NormalizedEvent
                {
                    Severity = severity,
                    SourceIp = "192.168.1.100",
                    DestinationIp = "8.8.8.8",
                    Protocol = Protocol.TCP
                };

                var result = _validator.Validate(evt);

                Assert.True(result.IsValid, $"Severity {severity} failed: {string.Join(", ", result.Errors)}");
            }
        }

        [Fact]
        public void Validate_EventWithAllProtocols_ReturnsValid()
        {
            foreach (Protocol protocol in Enum.GetValues(typeof(Protocol)))
            {
                var evt = new NormalizedEvent
                {
                    SourceIp = "192.168.1.100",
                    DestinationIp = "8.8.8.8",
                    Protocol = protocol
                };

                var result = _validator.Validate(evt);

                Assert.True(result.IsValid, $"Protocol {protocol} failed: {string.Join(", ", result.Errors)}");
            }
        }

        [Fact]
        public void Validate_EventWithIPv6Addresses_ReturnsValid()
        {
            var evt = new NormalizedEvent
            {
                SourceIp = "2001:0db8:85a3:0000:0000:8a2e:0370:7334",
                DestinationIp = "2001:0db8:85a3:0000:0000:8a2e:0370:7335",
                Protocol = Protocol.TCP
            };

            var result = _validator.Validate(evt);

            Assert.True(result.IsValid, string.Join(", ", result.Errors));
        }

        [Fact]
        public void Validate_EventWithBoundaryPorts_ReturnsValid()
        {
            var evt1 = new NormalizedEvent
            {
                SourceIp = "192.168.1.100",
                DestinationIp = "8.8.8.8",
                Protocol = Protocol.TCP,
                SourcePort = 0,
                DestinationPort = 0
            };

            var result1 = _validator.Validate(evt1);
            Assert.True(result1.IsValid, string.Join(", ", result1.Errors));

            var evt2 = new NormalizedEvent
            {
                SourceIp = "192.168.1.100",
                DestinationIp = "8.8.8.8",
                Protocol = Protocol.TCP,
                SourcePort = 65535,
                DestinationPort = 65535
            };

            var result2 = _validator.Validate(evt2);
            Assert.True(result2.IsValid, string.Join(", ", result2.Errors));
        }

        [Fact]
        public void Validate_NetworkEventWithValidHttpMethods_ReturnsValid()
        {
            var methods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "TRACE", "CONNECT" };

            foreach (var method in methods)
            {
                var evt = new NetworkEvent
                {
                    SourceIp = "192.168.1.100",
                    DestinationIp = "8.8.8.8",
                    Protocol = Protocol.HTTP,
                    HttpMethod = method
                };

                var result = _validator.Validate(evt);
                Assert.True(result.IsValid, $"HTTP method {method} failed: {string.Join(", ", result.Errors)}");
            }
        }

        [Fact]
        public void Validate_NetworkEventWithValidHttpStatusCodes_ReturnsValid()
        {
            var statusCodes = new[] { 100, 200, 201, 301, 302, 400, 401, 403, 404, 500, 502, 503 };

            foreach (var statusCode in statusCodes)
            {
                var evt = new NetworkEvent
                {
                    SourceIp = "192.168.1.100",
                    DestinationIp = "8.8.8.8",
                    Protocol = Protocol.HTTP,
                    HttpStatusCode = statusCode
                };

                var result = _validator.Validate(evt);
                Assert.True(result.IsValid, $"HTTP status code {statusCode} failed: {string.Join(", ", result.Errors)}");
            }
        }

        [Fact]
        public void ValidateJson_ValidJsonString_ReturnsValid()
        {
            var json = """
            {
                "id": "123e4567-e89b-12d3-a456-426614174000",
                "timestamp": "2024-11-04T10:30:00Z",
                "eventType": "networkTraffic",
                "severity": "info",
                "sourceIp": "192.168.1.100",
                "destinationIp": "8.8.8.8",
                "sourcePort": 54321,
                "destinationPort": 443,
                "protocol": "https",
                "metadata": {}
            }
            """;

            var result = _validator.ValidateJson(json);

            Assert.True(result.IsValid, string.Join(", ", result.Errors));
        }

        [Fact]
        public void Serialize_NormalizedEvent_ProducesValidJson()
        {
            var evt = new NormalizedEvent
            {
                Id = Guid.Parse("123e4567-e89b-12d3-a456-426614174000"),
                Timestamp = DateTime.Parse("2024-11-04T10:30:00Z").ToUniversalTime(),
                EventType = EventType.NetworkTraffic,
                Severity = Severity.Info,
                SourceIp = "192.168.1.100",
                DestinationIp = "8.8.8.8",
                Protocol = Protocol.HTTPS
            };

            var json = _validator.Serialize(evt);

            Assert.NotNull(json);
            Assert.Contains("123e4567-e89b-12d3-a456-426614174000", json);
            Assert.Contains("192.168.1.100", json);
            Assert.Contains("networkTraffic", json);
        }

        [Fact]
        public void Deserialize_ValidJson_ReturnsNormalizedEvent()
        {
            var json = """
            {
                "id": "123e4567-e89b-12d3-a456-426614174000",
                "timestamp": "2024-11-04T10:30:00Z",
                "eventType": "networkTraffic",
                "severity": "info",
                "sourceIp": "192.168.1.100",
                "destinationIp": "8.8.8.8",
                "sourcePort": 54321,
                "destinationPort": 443,
                "protocol": "https",
                "metadata": {}
            }
            """;

            var evt = _validator.Deserialize<NormalizedEvent>(json);

            Assert.NotNull(evt);
            Assert.Equal(Guid.Parse("123e4567-e89b-12d3-a456-426614174000"), evt.Id);
            Assert.Equal(EventType.NetworkTraffic, evt.EventType);
            Assert.Equal(Severity.Info, evt.Severity);
            Assert.Equal("192.168.1.100", evt.SourceIp);
            Assert.Equal("8.8.8.8", evt.DestinationIp);
            Assert.Equal(54321, evt.SourcePort);
            Assert.Equal(443, evt.DestinationPort);
            Assert.Equal(Protocol.HTTPS, evt.Protocol);
        }

        [Fact]
        public void Deserialize_ValidJson_ReturnsNetworkEvent()
        {
            var json = """
            {
                "id": "223e4567-e89b-12d3-a456-426614174001",
                "timestamp": "2024-11-04T10:30:15Z",
                "eventType": "networkTraffic",
                "severity": "info",
                "sourceIp": "192.168.1.100",
                "destinationIp": "93.184.216.34",
                "sourcePort": 54322,
                "destinationPort": 443,
                "protocol": "https",
                "bytesSent": 1024,
                "bytesReceived": 4096,
                "packetCount": 15,
                "sni": "example.com",
                "httpUrl": "https://example.com/api/data",
                "httpMethod": "GET",
                "httpStatusCode": 200,
                "userAgent": "Mozilla/5.0",
                "metadata": {}
            }
            """;

            var evt = _validator.Deserialize<NetworkEvent>(json);

            Assert.NotNull(evt);
            Assert.Equal(1024, evt.BytesSent);
            Assert.Equal(4096, evt.BytesReceived);
            Assert.Equal(15, evt.PacketCount);
            Assert.Equal("example.com", evt.Sni);
            Assert.Equal("https://example.com/api/data", evt.HttpUrl);
            Assert.Equal("GET", evt.HttpMethod);
            Assert.Equal(200, evt.HttpStatusCode);
            Assert.Equal("Mozilla/5.0", evt.UserAgent);
        }

        [Fact]
        public void SerializeDeserialize_NormalizedEvent_NoDataLoss()
        {
            var original = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = EventType.DnsQuery,
                Severity = Severity.Medium,
                SourceIp = "192.168.1.100",
                DestinationIp = "8.8.8.8",
                SourcePort = 54321,
                DestinationPort = 53,
                Protocol = Protocol.DNS,
                Payload = "example.com",
                DeviceName = "eth0",
                SensorId = "sensor-01"
            };

            var json = _validator.Serialize(original);
            var deserialized = _validator.Deserialize<NormalizedEvent>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.Timestamp, deserialized.Timestamp);
            Assert.Equal(original.EventType, deserialized.EventType);
            Assert.Equal(original.Severity, deserialized.Severity);
            Assert.Equal(original.SourceIp, deserialized.SourceIp);
            Assert.Equal(original.DestinationIp, deserialized.DestinationIp);
            Assert.Equal(original.SourcePort, deserialized.SourcePort);
            Assert.Equal(original.DestinationPort, deserialized.DestinationPort);
            Assert.Equal(original.Protocol, deserialized.Protocol);
            Assert.Equal(original.Payload, deserialized.Payload);
            Assert.Equal(original.DeviceName, deserialized.DeviceName);
            Assert.Equal(original.SensorId, deserialized.SensorId);
        }

        [Fact]
        public void SerializeDeserialize_NetworkEvent_NoDataLoss()
        {
            var original = new NetworkEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Severity = Severity.High,
                SourceIp = "192.168.1.100",
                DestinationIp = "93.184.216.34",
                SourcePort = 54322,
                DestinationPort = 443,
                Protocol = Protocol.HTTPS,
                BytesSent = 2048,
                BytesReceived = 8192,
                PacketCount = 25,
                Sni = "secure.example.com",
                HttpUrl = "https://secure.example.com/login",
                HttpMethod = "POST",
                HttpStatusCode = 302,
                UserAgent = "Custom/1.0",
                DeviceName = "wlan0",
                SensorId = "sensor-02",
                Metadata = new Dictionary<string, object>
                {
                    { "threat_level", "high" },
                    { "confidence", 0.95 }
                }
            };

            var json = _validator.Serialize(original);
            var deserialized = _validator.Deserialize<NetworkEvent>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.BytesSent, deserialized.BytesSent);
            Assert.Equal(original.BytesReceived, deserialized.BytesReceived);
            Assert.Equal(original.PacketCount, deserialized.PacketCount);
            Assert.Equal(original.Sni, deserialized.Sni);
            Assert.Equal(original.HttpUrl, deserialized.HttpUrl);
            Assert.Equal(original.HttpMethod, deserialized.HttpMethod);
            Assert.Equal(original.HttpStatusCode, deserialized.HttpStatusCode);
            Assert.Equal(original.UserAgent, deserialized.UserAgent);
            Assert.Equal(original.DeviceName, deserialized.DeviceName);
            Assert.Equal(original.SensorId, deserialized.SensorId);
        }

        [Fact]
        public void SerializeDeserialize_EventWithComplexMetadata_NoDataLoss()
        {
            var original = new NetworkEvent
            {
                SourceIp = "192.168.1.100",
                DestinationIp = "8.8.8.8",
                Protocol = Protocol.TCP,
                Metadata = new Dictionary<string, object>
                {
                    { "string_field", "value" },
                    { "int_field", 42 },
                    { "double_field", 3.14 },
                    { "bool_field", true }
                }
            };

            var json = _validator.Serialize(original);
            var validationResult = _validator.ValidateJson(json);

            Assert.True(validationResult.IsValid, string.Join(", ", validationResult.Errors));

            var deserialized = _validator.Deserialize<NetworkEvent>(json);

            Assert.NotNull(deserialized);
            Assert.NotEmpty(deserialized.Metadata);
        }
    }
}
