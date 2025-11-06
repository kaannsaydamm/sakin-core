using FluentAssertions;
using Sakin.HttpCollector.Configuration;
using Xunit;

namespace Sakin.HttpCollector.Tests.Unit;

public class ConfigurationTests
{
    [Fact]
    public void HttpCollectorOptions_ShouldHaveDefaultValues()
    {
        var options = new HttpCollectorOptions();

        options.Port.Should().Be(8080);
        options.Path.Should().Be("/api/events");
        options.MaxBodySize.Should().Be(65536);
        options.ValidApiKeys.Should().BeEmpty();
        options.RequireApiKey.Should().BeFalse();
    }

    [Fact]
    public void KafkaPublisherOptions_ShouldHaveDefaultValues()
    {
        var options = new KafkaPublisherOptions();

        options.BootstrapServers.Should().Be("kafka:9092");
        options.Topic.Should().Be("raw-events");
    }

    [Fact]
    public void HttpCollectorOptions_SectionName_ShouldBeCorrect()
    {
        HttpCollectorOptions.SectionName.Should().Be("HttpCollector");
    }

    [Fact]
    public void KafkaPublisherOptions_SectionName_ShouldBeCorrect()
    {
        KafkaPublisherOptions.SectionName.Should().Be("Kafka");
    }
}
