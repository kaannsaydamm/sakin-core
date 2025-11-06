using FluentAssertions;
using Sakin.HttpCollector.Models;
using Xunit;

namespace Sakin.HttpCollector.Tests.Unit;

public class RawLogEntryTests
{
    [Fact]
    public void RawLogEntry_ShouldCreateWithRequiredProperties()
    {
        var entry = new RawLogEntry
        {
            RawMessage = "CEF:0|Vendor|Product|1.0|100|Test|5|",
            SourceIp = "192.168.1.100",
            ContentType = "text/plain"
        };

        entry.RawMessage.Should().Be("CEF:0|Vendor|Product|1.0|100|Test|5|");
        entry.SourceIp.Should().Be("192.168.1.100");
        entry.ContentType.Should().Be("text/plain");
        entry.ReceivedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RawLogEntry_ShouldSupportXSourceHeader()
    {
        var entry = new RawLogEntry
        {
            RawMessage = "test message",
            SourceIp = "192.168.1.100",
            ContentType = "text/plain",
            XSourceHeader = "firewall-01"
        };

        entry.XSourceHeader.Should().Be("firewall-01");
    }

    [Fact]
    public void RawLogEntry_ShouldAllowCustomReceivedAt()
    {
        var customTime = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        
        var entry = new RawLogEntry
        {
            RawMessage = "test message",
            SourceIp = "192.168.1.100",
            ContentType = "text/plain",
            ReceivedAt = customTime
        };

        entry.ReceivedAt.Should().Be(customTime);
    }
}
