using FluentAssertions;
using Sakin.Common.Models;
using Sakin.Ingest.Parsers;
using Xunit;

namespace Sakin.Ingest.Tests.Parsers;

public class FortinetLogParserTests
{
    private readonly FortinetLogParser _parser = new();

    [Fact]
    public void SourceType_ReturnsFortinet()
    {
        _parser.SourceType.Should().Be("fortinet");
    }

    [Fact]
    public async Task ParseAsync_CefLog_ExtractsFields()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "fortinet-gateway",
            SourceType = "fortinet",
            Raw = TestFixtures.FortinetCefLog
        };

        var result = await _parser.ParseAsync(envelope);

        result.Should().NotBeNull();
        result.SourceIp.Should().Be("203.0.113.45");
        result.DestinationIp.Should().Be("192.168.1.1");
        result.SourcePort.Should().Be(54321);
        result.DestinationPort.Should().Be(443);
        result.Protocol.Should().Be(Protocol.TCP);
        result.EventType.Should().Be(EventType.NetworkTraffic);
    }

    [Fact]
    public async Task ParseAsync_KeyValueLog_ExtractsFields()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "fortinet-gateway",
            SourceType = "fortinet",
            Raw = TestFixtures.FortinetKeyValueLog
        };

        var result = await _parser.ParseAsync(envelope);

        result.Should().NotBeNull();
        result.SourceIp.Should().Be("192.168.1.100");
        result.DestinationIp.Should().Be("8.8.8.8");
        result.SourcePort.Should().Be(54321);
        result.DestinationPort.Should().Be(53);
        result.Protocol.Should().Be(Protocol.UDP);
        result.Metadata.Should().ContainKey("action");
        result.Metadata["action"].Should().Be("accept");
        result.Metadata.Should().ContainKey("policy_id");
    }

    [Fact]
    public async Task ParseAsync_DenyLog_SetsSeverityAndEventType()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "fortinet-gateway",
            SourceType = "fortinet",
            Raw = TestFixtures.FortinetDenyLog
        };

        var result = await _parser.ParseAsync(envelope);

        result.Should().NotBeNull();
        result.Metadata.Should().ContainKey("action");
        result.Metadata["action"].Should().Be("deny");
        result.EventType.Should().Be(EventType.SecurityAlert);
        ((int)result.Severity).Should().BeGreaterThanOrEqualTo((int)Severity.Medium);
    }

    [Fact]
    public async Task ParseAsync_InvalidLog_ReturnsNormalizedEvent()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "fortinet-gateway",
            SourceType = "fortinet",
            Raw = "invalid fortinet log"
        };

        var result = await _parser.ParseAsync(envelope);

        result.Should().NotBeNull();
        result.EventType.Should().Be(EventType.NetworkTraffic);
    }

    [Fact]
    public async Task ParseAsync_ExtractsAllIpFields()
    {
        var log = "action=accept srcip=10.0.0.1 dstip=192.168.1.1 srcport=12345 dstport=443 proto=6";

        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "fortinet-gateway",
            SourceType = "fortinet",
            Raw = log
        };

        var result = await _parser.ParseAsync(envelope);

        result.SourceIp.Should().Be("10.0.0.1");
        result.DestinationIp.Should().Be("192.168.1.1");
        result.Protocol.Should().Be(Protocol.TCP);
    }
}
