using FluentAssertions;
using Sakin.Common.Models;
using Sakin.Ingest.Parsers;
using Xunit;

namespace Sakin.Ingest.Tests.Parsers;

public class WindowsEventLogParserTests
{
    private readonly WindowsEventLogParser _parser = new();

    [Fact]
    public void SourceType_ReturnsWindowsEventlog()
    {
        _parser.SourceType.Should().Be("windows-eventlog");
    }

    [Fact]
    public async Task ParseAsync_LoginSuccessEvent_ExtractsEventDetails()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "win-sensor-01",
            SourceType = "windows-eventlog",
            Raw = TestFixtures.WindowsEventLogLoginSuccess
        };

        var result = await _parser.ParseAsync(envelope);

        result.EventType.Should().Be(EventType.AuthenticationAttempt);
        result.Severity.Should().Be(Severity.Info);
        result.Metadata.Should().ContainKey("event_id");
        result.Metadata["event_id"].Should().Be(4624U);
        result.Metadata.Should().ContainKey("action");
        result.Metadata["action"].Should().Be("login_success");
        result.Metadata.Should().ContainKey("username");
        result.Metadata["username"].Should().Be("admin");
        result.DeviceName.Should().Be("DESKTOP-ABC123");
        result.SourceIp.Should().Be("192.168.1.50");
    }

    [Fact]
    public async Task ParseAsync_LoginFailedEvent_ExtractsEventDetails()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "win-sensor-02",
            SourceType = "windows-eventlog",
            Raw = TestFixtures.WindowsEventLogLoginFailed
        };

        var result = await _parser.ParseAsync(envelope);

        result.EventType.Should().Be(EventType.AuthenticationAttempt);
        result.Severity.Should().Be(Severity.Medium);
        result.Metadata.Should().ContainKey("event_id");
        result.Metadata["event_id"].Should().Be(4625U);
        result.Metadata.Should().ContainKey("action");
        result.Metadata["action"].Should().Be("login_failed");
        result.Metadata.Should().ContainKey("username");
        result.Metadata["username"].Should().Be("attacker");
        result.DeviceName.Should().Be("DESKTOP-XYZ789");
    }

    [Fact]
    public async Task ParseAsync_InvalidXml_ReturnsNormalizedEventWithError()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "win-sensor-03",
            SourceType = "windows-eventlog",
            Raw = "invalid xml content"
        };

        var result = await _parser.ParseAsync(envelope);

        result.Metadata.Should().ContainKey("parse_error");
        result.EventType.Should().Be(EventType.SystemLog);
    }

    [Fact]
    public async Task ParseAsync_EmptyRawData_ReturnsNormalizedEvent()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "win-sensor-04",
            SourceType = "windows-eventlog",
            Raw = string.Empty
        };

        var result = await _parser.ParseAsync(envelope);

        result.Should().NotBeNull();
        result.EventType.Should().Be(EventType.SystemLog);
    }
}
