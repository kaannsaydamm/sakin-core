using FluentAssertions;
using Sakin.Common.Models;
using Sakin.Ingest.Parsers;
using Xunit;

namespace Sakin.Ingest.Tests.Parsers;

public class SyslogParserTests
{
    private readonly SyslogParser _parser = new();

    [Fact]
    public void SourceType_ReturnsSyslog()
    {
        _parser.SourceType.Should().Be("syslog");
    }

    [Fact]
    public async Task ParseAsync_Rfc5424Message_ExtractsFields()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "syslog-server",
            SourceType = "syslog",
            Raw = TestFixtures.Rfc5424SyslogMessage
        };

        var result = await _parser.ParseAsync(envelope);

        result.Should().NotBeNull();
        result.Metadata.Should().ContainKey("message");
        result.SourceIp.Should().Be("192.168.1.50");
    }

    [Fact]
    public async Task ParseAsync_Rfc3164Message_ExtractsFields()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "syslog-server",
            SourceType = "syslog",
            Raw = TestFixtures.Rfc3164SyslogMessage
        };

        var result = await _parser.ParseAsync(envelope);

        result.Should().NotBeNull();
        result.DeviceName.Should().Be("hostname");
        result.Metadata.Should().ContainKey("tag");
        result.Metadata["tag"].Should().Be("sudo");
    }

    [Fact]
    public async Task ParseAsync_SshFailedLogin_DetectsEventType()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "syslog-server",
            SourceType = "syslog",
            Raw = TestFixtures.SshFailedLoginSyslog
        };

        var result = await _parser.ParseAsync(envelope);

        result.EventType.Should().Be(EventType.SshConnection);
        ((int)result.Severity).Should().BeGreaterThanOrEqualTo((int)Severity.Medium);
    }

    [Fact]
    public async Task ParseAsync_ExtractsSourceIp()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "syslog-server",
            SourceType = "syslog",
            Raw = "Jan 15 10:31:00 host sshd[1234]: Failed password for user admin from 203.0.113.45 port 12345"
        };

        var result = await _parser.ParseAsync(envelope);

        result.SourceIp.Should().Be("203.0.113.45");
    }

    [Fact]
    public async Task ParseAsync_InvalidSyslog_StillReturnsNormalizedEvent()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "syslog-server",
            SourceType = "syslog",
            Raw = "random log message without standard format"
        };

        var result = await _parser.ParseAsync(envelope);

        result.Should().NotBeNull();
        result.EventType.Should().Be(EventType.SystemLog);
    }
}
