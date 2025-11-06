using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Confluent.Kafka;
using FluentAssertions;
using Sakin.Common.Models;
using Sakin.Integration.Tests.Fixtures;
using Sakin.Integration.Tests.Helpers;
using Xunit;

namespace Sakin.Integration.Tests.Scenarios;

[Collection("Integration Tests")]
public class EventIngestionScenarioTests
{
    private readonly IntegrationTestFixture _fixture;

    public EventIngestionScenarioTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Scenario 1: Simple Event Ingestion & Normalization")]
    public async Task SimpleEventIngestionAndNormalization()
    {
        // Arrange
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("raw-events");
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("normalized-events");

        var syslogEvent = EventFactory.CreateSyslogEvent(
            sourceIp: "10.0.0.50",
            hostname: "test-host",
            message: "Test syslog message");

        // Act - Produce raw event to raw-events topic
        var producer = await _fixture.KafkaFixture.CreateProducerAsync<RawEvent>();
        await producer.ProduceAsync("raw-events", new Message<string, RawEvent>
        {
            Key = syslogEvent.Source,
            Value = syslogEvent
        });
        producer.Flush(TimeSpan.FromSeconds(5));

        // Act - Consume from normalized-events topic
        var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<NormalizedEvent>(
            "test-ingestion-group",
            new[] { "normalized-events" });

        var normalizedEvent = await AssertionHelpers.WaitForMessageAsync(consumer, timeout: TimeSpan.FromSeconds(30));

        // Assert
        normalizedEvent.Should().NotBeNull("Normalized event should be produced to Kafka");
        normalizedEvent!.Timestamp.Should().BeCloseTo(syslogEvent.Timestamp, precision: TimeSpan.FromSeconds(1));
        normalizedEvent.SourceIp.Should().Be(syslogEvent.Source);
        normalizedEvent.EventPayload.Should().NotBeEmpty("Event payload should contain parsed data");

        // Assert GeoIP enrichment (should have location data if available)
        // GeoLocation can be null for test IPs, so we just verify the event was created
        normalizedEvent.EventType.Should().NotBeNullOrEmpty("Event type should be set");

        // Cleanup
        producer.Dispose();
        consumer.Dispose();
    }

    [Fact(DisplayName = "Scenario 1.2: Multiple Event Sources Normalization")]
    public async Task MultipleEventSourcesNormalization()
    {
        // Arrange
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("raw-events");
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("normalized-events");

        var windowsEvent = EventFactory.CreateWindowsEventLogEvent(eventCode: 4625);
        var syslogEvent = EventFactory.CreateSyslogEvent();
        var cefEvent = EventFactory.CreateHTTPCEFEvent();

        var rawEvents = new[] { windowsEvent, syslogEvent, cefEvent };

        // Act - Produce raw events
        var producer = await _fixture.KafkaFixture.CreateProducerAsync<RawEvent>();

        foreach (var rawEvent in rawEvents)
        {
            await producer.ProduceAsync("raw-events", new Message<string, RawEvent>
            {
                Key = rawEvent.Source,
                Value = rawEvent
            });
        }

        producer.Flush(TimeSpan.FromSeconds(5));

        // Act - Consume normalized events
        var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<NormalizedEvent>(
            "test-multi-source-group",
            new[] { "normalized-events" });

        var normalizedEvents = await AssertionHelpers.WaitForMessagesAsync(
            consumer,
            expectedCount: rawEvents.Length,
            timeout: TimeSpan.FromSeconds(30));

        // Assert
        normalizedEvents.Should().HaveCount(rawEvents.Length, "All raw events should be normalized");

        foreach (var normalizedEvent in normalizedEvents)
        {
            normalizedEvent.Timestamp.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromMinutes(1));
            normalizedEvent.SourceIp.Should().NotBeNullOrEmpty("Source IP should be extracted");
            normalizedEvent.EventType.Should().NotBeNullOrEmpty("Event type should be determined");
        }

        // Cleanup
        producer.Dispose();
        consumer.Dispose();
    }

    [Fact(DisplayName = "Scenario 1.3: Timestamp Parsing Accuracy")]
    public async Task TimestampParsingAccuracy()
    {
        // Arrange
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("raw-events");
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("normalized-events");

        var timestamp = DateTime.UtcNow.AddHours(-5);
        var syslogEvent = new RawEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = timestamp,
            Source = "10.0.0.50",
            SourceType = "Syslog",
            Payload = new Dictionary<string, object>
            {
                { "hostname", "test-host" },
                { "message", "Test with specific timestamp" }
            },
            RawPayload = "Test raw payload"
        };

        // Act
        var producer = await _fixture.KafkaFixture.CreateProducerAsync<RawEvent>();
        await producer.ProduceAsync("raw-events", new Message<string, RawEvent>
        {
            Key = syslogEvent.Source,
            Value = syslogEvent
        });
        producer.Flush(TimeSpan.FromSeconds(5));

        var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<NormalizedEvent>(
            "test-timestamp-group",
            new[] { "normalized-events" });

        var normalizedEvent = await AssertionHelpers.WaitForMessageAsync(consumer, timeout: TimeSpan.FromSeconds(30));

        // Assert - timestamp should be parsed correctly (within 1 minute tolerance for processing)
        normalizedEvent.Should().NotBeNull();
        normalizedEvent!.Timestamp.Should().BeCloseTo(timestamp, precision: TimeSpan.FromMinutes(1),
            "Timestamp should be accurately parsed");

        // Cleanup
        producer.Dispose();
        consumer.Dispose();
    }

    [Fact(DisplayName = "Scenario 1.4: No Data Loss Verification")]
    public async Task NoDataLossVerification()
    {
        // Arrange
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("raw-events");
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("normalized-events");

        const int eventCount = 20;
        var rawEvents = new List<RawEvent>();

        for (int i = 0; i < eventCount; i++)
        {
            rawEvents.Add(EventFactory.CreateSyslogEvent(
                sourceIp: $"10.0.0.{50 + (i % 10)}",
                hostname: $"host-{i}",
                message: $"Message {i}"));
        }

        // Act
        var producer = await _fixture.KafkaFixture.CreateProducerAsync<RawEvent>();

        foreach (var rawEvent in rawEvents)
        {
            await producer.ProduceAsync("raw-events", new Message<string, RawEvent>
            {
                Key = rawEvent.Source,
                Value = rawEvent
            });
        }

        producer.Flush(TimeSpan.FromSeconds(5));

        // Act - Consume all normalized events
        var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<NormalizedEvent>(
            "test-no-loss-group",
            new[] { "normalized-events" });

        var normalizedEvents = await AssertionHelpers.WaitForMessagesAsync(
            consumer,
            expectedCount: eventCount,
            timeout: TimeSpan.FromSeconds(60));

        // Assert
        AssertionHelpers.AssertNoDataLoss(rawEvents, normalizedEvents, "events");

        // Cleanup
        producer.Dispose();
        consumer.Dispose();
    }
}
