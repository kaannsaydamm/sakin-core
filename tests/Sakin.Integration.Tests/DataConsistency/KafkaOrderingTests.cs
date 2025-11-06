using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using FluentAssertions;
using Sakin.Common.Models;
using Sakin.Integration.Tests.Fixtures;
using Sakin.Integration.Tests.Helpers;
using Xunit;

namespace Sakin.Integration.Tests.DataConsistency;

[Collection("Integration Tests")]
public class KafkaOrderingTests
{
    private readonly IntegrationTestFixture _fixture;

    public KafkaOrderingTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Kafka Message Ordering - Same Source Partition")]
    public async Task KafkaMessageOrderingSameSourcePartition()
    {
        // Arrange
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("test-ordering", partitions: 1);

        const string sourceIp = "192.168.1.100";
        const int messageCount = 50;

        var events = new List<NormalizedEvent>();
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < messageCount; i++)
        {
            events.Add(new NormalizedEvent
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = baseTime.AddSeconds(i),
                EventType = "test-event",
                Severity = "low",
                SourceIp = sourceIp,
                DestinationIp = "192.168.1.1",
                SourcePort = 50000 + i,
                DestinationPort = 443,
                Protocol = "TCP",
                Username = null,
                Hostname = null,
                DeviceName = "TEST",
                EventPayload = new Dictionary<string, object> { { "sequence", i } },
                GeoLocation = new GeoLocationData { CountryCode = "US" }
            });
        }

        // Act
        var producer = await _fixture.KafkaFixture.CreateProducerAsync<NormalizedEvent>();

        foreach (var @event in events)
        {
            await producer.ProduceAsync("test-ordering", new Message<string, NormalizedEvent>
            {
                Key = sourceIp,
                Value = @event
            });
        }

        producer.Flush(TimeSpan.FromSeconds(5));

        var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<NormalizedEvent>(
            "test-ordering-group",
            new[] { "test-ordering" });

        var receivedEvents = await AssertionHelpers.WaitForMessagesAsync(
            consumer,
            expectedCount: messageCount,
            timeout: TimeSpan.FromSeconds(30));

        // Assert
        receivedEvents.Should().HaveCount(messageCount);

        AssertionHelpers.AssertMessageOrdering(
            receivedEvents,
            e => (int)e.EventPayload["sequence"]);

        // Cleanup
        producer.Dispose();
        consumer.Dispose();
    }

    [Fact(DisplayName = "Kafka Ordering - Multiple Partitions Different Sources")]
    public async Task KafkaOrderingMultiplePartitionsDifferentSources()
    {
        // Arrange
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("test-multi-partition", partitions: 3);

        const int eventsPerSource = 10;
        var sources = new[] { "192.168.1.100", "192.168.1.200", "192.168.1.300" };

        var allEvents = new List<(string Source, NormalizedEvent Event)>();
        var baseTime = DateTime.UtcNow;

        foreach (var source in sources)
        {
            for (int i = 0; i < eventsPerSource; i++)
            {
                allEvents.Add((source, new NormalizedEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    Timestamp = baseTime.AddSeconds(i),
                    EventType = "test-event",
                    Severity = "low",
                    SourceIp = source,
                    DestinationIp = "192.168.1.1",
                    SourcePort = 50000 + i,
                    DestinationPort = 443,
                    Protocol = "TCP",
                    Username = null,
                    Hostname = null,
                    DeviceName = "TEST",
                    EventPayload = new Dictionary<string, object> { { "sequence", i } },
                    GeoLocation = new GeoLocationData { CountryCode = "US" }
                }));
            }
        }

        // Act
        var producer = await _fixture.KafkaFixture.CreateProducerAsync<NormalizedEvent>();

        foreach (var (source, @event) in allEvents)
        {
            await producer.ProduceAsync("test-multi-partition", new Message<string, NormalizedEvent>
            {
                Key = source,
                Value = @event
            });
        }

        producer.Flush(TimeSpan.FromSeconds(5));

        var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<NormalizedEvent>(
            "test-multi-partition-group",
            new[] { "test-multi-partition" });

        var receivedEvents = await AssertionHelpers.WaitForMessagesAsync(
            consumer,
            expectedCount: sources.Length * eventsPerSource,
            timeout: TimeSpan.FromSeconds(30));

        // Assert
        receivedEvents.Should().HaveCount(sources.Length * eventsPerSource);

        // Verify ordering within each source partition
        foreach (var source in sources)
        {
            var sourceEvents = receivedEvents.Where(e => e.SourceIp == source).ToList();
            sourceEvents.Should().HaveCount(eventsPerSource);

            AssertionHelpers.AssertMessageOrdering(
                sourceEvents,
                e => (int)e.EventPayload["sequence"]);
        }

        // Cleanup
        producer.Dispose();
        consumer.Dispose();
    }

    [Fact(DisplayName = "Kafka Replay - Message Idempotency")]
    public async Task KafkaReplayMessageIdempotency()
    {
        // Arrange
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("test-replay", partitions: 1);

        var testEvent = EventFactory.CreateNormalizedEvent();

        // Act - Produce same event twice
        var producer = await _fixture.KafkaFixture.CreateProducerAsync<NormalizedEvent>();

        await producer.ProduceAsync("test-replay", new Message<string, NormalizedEvent>
        {
            Key = testEvent.SourceIp,
            Value = testEvent
        });

        await producer.ProduceAsync("test-replay", new Message<string, NormalizedEvent>
        {
            Key = testEvent.SourceIp,
            Value = testEvent
        });

        producer.Flush(TimeSpan.FromSeconds(5));

        // Consume both messages
        var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<NormalizedEvent>(
            "test-replay-group",
            new[] { "test-replay" });

        var receivedEvents = await AssertionHelpers.WaitForMessagesAsync(
            consumer,
            expectedCount: 2,
            timeout: TimeSpan.FromSeconds(30));

        // Assert
        receivedEvents.Should().HaveCount(2);

        var serializedOriginal = System.Text.Json.JsonSerializer.Serialize(testEvent);
        var serializedReceived = System.Text.Json.JsonSerializer.Serialize(receivedEvents[0]);

        AssertionHelpers.AssertIdempotency(
            testEvent,
            receivedEvents[0],
            e => System.Text.Json.JsonSerializer.Serialize(e));

        // Cleanup
        producer.Dispose();
        consumer.Dispose();
    }
}
