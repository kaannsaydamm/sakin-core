using Sakin.Common.Models;
using Sakin.Common.Serialization;
using System.IO;
using Xunit;

namespace Sakin.Common.Tests.Fixtures
{
    public class EventEnvelopeFixtureTests
    {
        [Fact]
        public void NetworkEventFixture_DeserializesCorrectly()
        {
            var fixturePath = Path.Combine("..", "..", "..", "..", "..", "tests", "fixtures", "event-envelope-network.json");
            var json = File.ReadAllText(fixturePath);

            var envelope = EventEnvelopeSerializer.Deserialize(json);

            Assert.Equal("sensor-01", envelope.Source);
            Assert.Equal("network-sensor", envelope.SourceType);
            Assert.NotNull(envelope.Normalized);
            Assert.Equal(EventType.NetworkTraffic, envelope.Normalized.EventType);
            Assert.Equal("192.168.1.100", envelope.Normalized.SourceIp);
            Assert.Equal("203.0.113.1", envelope.Normalized.DestinationIp);
            Assert.Equal(Protocol.TCP, envelope.Normalized.Protocol);
            Assert.True(envelope.Enrichment.ContainsKey("geoLocation"));
            Assert.Equal("v1.0", envelope.SchemaVersion);
        }

        [Fact]
        public void AuthEventFixture_DeserializesCorrectly()
        {
            var fixturePath = Path.Combine("..", "..", "..", "..", "..", "tests", "fixtures", "event-envelope-auth.json");
            var json = File.ReadAllText(fixturePath);

            var envelope = EventEnvelopeSerializer.Deserialize(json);

            Assert.Equal("win-sensor-02", envelope.Source);
            Assert.Equal("windows-eventlog", envelope.SourceType);
            Assert.NotNull(envelope.Normalized);
            Assert.Equal(EventType.AuthenticationAttempt, envelope.Normalized.EventType);
            Assert.Equal(Severity.Info, envelope.Normalized.Severity);
            Assert.Equal("User admin successfully logged on from workstation WIN-01", envelope.Normalized.Payload);
            Assert.True(envelope.Normalized.Metadata.ContainsKey("eventId"));
            Assert.Equal("4624", envelope.Normalized.Metadata["eventId"].ToString());
            Assert.True(envelope.Enrichment.ContainsKey("userRisk"));
            Assert.Equal("v1.0", envelope.SchemaVersion);
        }

        [Fact]
        public void DnsEventFixture_DeserializesCorrectly()
        {
            var fixturePath = Path.Combine("..", "..", "..", "..", "..", "tests", "fixtures", "event-envelope-dns.json");
            var json = File.ReadAllText(fixturePath);

            var envelope = EventEnvelopeSerializer.Deserialize(json);

            Assert.Equal("sensor-01", envelope.Source);
            Assert.Equal("network-sensor", envelope.SourceType);
            Assert.NotNull(envelope.Normalized);
            Assert.Equal(EventType.DnsQuery, envelope.Normalized.EventType);
            Assert.Equal("192.168.1.100", envelope.Normalized.SourceIp);
            Assert.Equal("8.8.8.8", envelope.Normalized.DestinationIp);
            Assert.Equal(Protocol.UDP, envelope.Normalized.Protocol);
            Assert.Equal(53, envelope.Normalized.DestinationPort);
            Assert.True(envelope.Normalized.Metadata.ContainsKey("queryType"));
            Assert.Equal("A", envelope.Normalized.Metadata["queryType"].ToString());
            Assert.True(envelope.Enrichment.ContainsKey("dnsCategory"));
            Assert.Equal("v1.0", envelope.SchemaVersion);
        }

        [Fact]
        public void AllFixtures_RoundTripSerialization()
        {
            var fixtureFiles = new[]
            {
                "event-envelope-network.json",
                "event-envelope-auth.json",
                "event-envelope-dns.json"
            };

            foreach (var fixtureFile in fixtureFiles)
            {
                var fixturePath = Path.Combine("..", "..", "..", "..", "..", "tests", "fixtures", fixtureFile);
                var originalJson = File.ReadAllText(fixturePath);

                var envelope = EventEnvelopeSerializer.Deserialize(originalJson);
                var serializedJson = EventEnvelopeSerializer.Serialize(envelope);
                var deserializedEnvelope = EventEnvelopeSerializer.Deserialize(serializedJson);

                Assert.Equal(envelope.EventId, deserializedEnvelope.EventId);
                Assert.Equal(envelope.Source, deserializedEnvelope.Source);
                Assert.Equal(envelope.SourceType, deserializedEnvelope.SourceType);
                Assert.Equal(envelope.SchemaVersion, deserializedEnvelope.SchemaVersion);
                Assert.Equal(envelope.Normalized?.EventType, deserializedEnvelope.Normalized?.EventType);
            }
        }
    }
}