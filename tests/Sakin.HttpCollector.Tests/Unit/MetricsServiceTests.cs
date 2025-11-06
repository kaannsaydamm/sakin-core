using FluentAssertions;
using Sakin.HttpCollector.Services;
using Xunit;

namespace Sakin.HttpCollector.Tests.Unit;

public class MetricsServiceTests
{
    [Fact]
    public void IncrementHttpRequests_ShouldNotThrow()
    {
        var service = new MetricsService();

        var act = () => service.IncrementHttpRequests("192.168.1.100", "cef_string", 202);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordHttpRequestDuration_ShouldNotThrow()
    {
        var service = new MetricsService();

        var act = () => service.RecordHttpRequestDuration(0.123);

        act.Should().NotThrow();
    }

    [Fact]
    public void IncrementHttpErrors_ShouldNotThrow()
    {
        var service = new MetricsService();

        var act = () => service.IncrementHttpErrors(400);

        act.Should().NotThrow();
    }

    [Fact]
    public void IncrementKafkaMessagesPublished_ShouldNotThrow()
    {
        var service = new MetricsService();

        var act = () => service.IncrementKafkaMessagesPublished("raw-events");

        act.Should().NotThrow();
    }
}
