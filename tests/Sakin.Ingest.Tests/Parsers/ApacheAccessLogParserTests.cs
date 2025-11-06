using FluentAssertions;
using Sakin.Common.Models;
using Sakin.Ingest.Parsers;
using Xunit;

namespace Sakin.Ingest.Tests.Parsers;

public class ApacheAccessLogParserTests
{
    private readonly ApacheAccessLogParser _parser = new();

    [Fact]
    public void SourceType_ReturnsApache()
    {
        _parser.SourceType.Should().Be("apache");
    }

    [Fact]
    public async Task ParseAsync_SuccessfulRequest_ExtractsFields()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "apache-server",
            SourceType = "apache",
            Raw = TestFixtures.ApacheAccessLogCombined
        };

        var result = await _parser.ParseAsync(envelope);

        result.Should().NotBeNull();
        result.Metadata.Should().ContainKey("http_method");
        result.Metadata.Should().ContainKey("http_status");
        result.Metadata.Should().ContainKey("path");
        result.Metadata.Should().ContainKey("user_agent");
        result.Protocol.Should().Be(Protocol.HTTP);
        result.EventType.Should().Be(EventType.HttpRequest);
        result.Severity.Should().Be(Severity.Info);
    }

    [Fact]
    public async Task ParseAsync_NotFoundError_ExtractsFields()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "apache-server",
            SourceType = "apache",
            Raw = TestFixtures.ApacheAccessLogError404
        };

        var result = await _parser.ParseAsync(envelope);

        result.Should().NotBeNull();
        result.SourceIp.Should().Be("10.0.0.50");
        result.Metadata.Should().ContainKey("http_status");
        result.Metadata["http_status"].Should().Be(404);
        result.Severity.Should().Be(Severity.Low);
    }

    [Fact]
    public async Task ParseAsync_ServerError_ExtractsFields()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "apache-server",
            SourceType = "apache",
            Raw = TestFixtures.ApacheAccessLogError500
        };

        var result = await _parser.ParseAsync(envelope);

        result.Should().NotBeNull();
        result.SourceIp.Should().Be("203.0.113.1");
        result.Metadata.Should().ContainKey("http_status");
        result.Metadata["http_status"].Should().Be(500);
        result.EventType.Should().Be(EventType.SecurityAlert);
        result.Severity.Should().Be(Severity.High);
    }

    [Fact]
    public async Task ParseAsync_InvalidLog_ReturnsNormalizedEvent()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "apache-server",
            SourceType = "apache",
            Raw = "invalid apache log format"
        };

        var result = await _parser.ParseAsync(envelope);

        result.Should().NotBeNull();
        result.EventType.Should().Be(EventType.HttpRequest);
    }

    [Fact]
    public async Task ParseAsync_ExtractsHttpMethod()
    {
        var log = """
            192.168.1.1 - - [15/Jan/2024:10:31:00 +0000] "POST /api/data HTTP/1.1" 201 256 "-" "curl"
            """.Trim();

        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "apache-server",
            SourceType = "apache",
            Raw = log
        };

        var result = await _parser.ParseAsync(envelope);

        result.Metadata.Should().ContainKey("http_method");
        result.Metadata["http_method"].Should().Be("POST");
    }
}
