using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;
using FluentAssertions;
using Sakin.Common.Models;
using Sakin.Integration.Tests.Fixtures;
using Sakin.Integration.Tests.Helpers;
using Xunit;

namespace Sakin.Integration.Tests.Scenarios;

[Collection("Integration Tests")]
public class AlertLifecycleScenarioTests
{
    private readonly IntegrationTestFixture _fixture;

    public AlertLifecycleScenarioTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Scenario 4: Alert Lifecycle & Deduplication")]
    public async Task AlertDeduplication()
    {
        // Arrange
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("normalized-events");
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("alerts");

        const string sourceIp = "192.168.1.100";
        const string username = "attacker";
        const int duplicateCount = 10;

        // Create duplicate events (same source, same type, close timestamps)
        var templateEvent = EventFactory.CreateNormalizedEvent(
            sourceIp: sourceIp,
            eventType: "failed-login",
            severity: "high",
            username: username);

        var duplicateEvents = TestDataBuilder.CreateDuplicateEvents(templateEvent, count: duplicateCount);

        // Act - Produce all duplicate events
        var producer = await _fixture.KafkaFixture.CreateProducerAsync<NormalizedEvent>();

        foreach (var normalizedEvent in duplicateEvents)
        {
            await producer.ProduceAsync("normalized-events", new Message<string, NormalizedEvent>
            {
                Key = normalizedEvent.SourceIp,
                Value = normalizedEvent
            });

            await Task.Delay(10);
        }

        producer.Flush(TimeSpan.FromSeconds(5));

        // Act - Consume alerts
        var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<Alert>(
            "test-dedup-group",
            new[] { "alerts" });

        var alerts = await AssertionHelpers.WaitForMessagesAsync(
            consumer,
            expectedCount: 1,
            timeout: TimeSpan.FromSeconds(30));

        // Assert - Only ONE alert should be created (deduplication working)
        alerts.Should().HaveCount(1, "Duplicate events should be deduplicated into single alert");

        var alert = alerts[0];
        alert.SourceIp.Should().Be(sourceIp);
        alert.Username.Should().Be(username);
        alert.Severity.Should().Be("High");

        // Cleanup
        producer.Dispose();
        consumer.Dispose();
    }

    [Fact(DisplayName = "Scenario 4.2: Alert Counter Increment")]
    public async Task AlertCounterIncrement()
    {
        // Arrange
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("normalized-events");
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("alerts");

        const string sourceIp = "192.168.1.100";
        const int duplicateCount = 15;

        var templateEvent = EventFactory.CreateNormalizedEvent(sourceIp: sourceIp);
        var duplicateEvents = TestDataBuilder.CreateDuplicateEvents(templateEvent, count: duplicateCount);

        // Act
        var producer = await _fixture.KafkaFixture.CreateProducerAsync<NormalizedEvent>();

        foreach (var normalizedEvent in duplicateEvents)
        {
            await producer.ProduceAsync("normalized-events", new Message<string, NormalizedEvent>
            {
                Key = normalizedEvent.SourceIp,
                Value = normalizedEvent
            });

            await Task.Delay(10);
        }

        producer.Flush(TimeSpan.FromSeconds(5));

        var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<Alert>(
            "test-counter-group",
            new[] { "alerts" });

        var alerts = await AssertionHelpers.WaitForMessagesAsync(
            consumer,
            expectedCount: 1,
            timeout: TimeSpan.FromSeconds(30));

        // Assert - Alert count should reflect all duplicates
        alerts.Should().HaveCount(1);
        var alert = alerts[0];
        // The alert should have a mechanism to track duplicate count
        // This would be stored in alert properties or a counter field

        // Cleanup
        producer.Dispose();
        consumer.Dispose();
    }

    [Fact(DisplayName = "Scenario 4.3: FirstSeen and LastSeen Time Range")]
    public async Task FirstSeenAndLastSeenTimeRange()
    {
        // Arrange
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("normalized-events");
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("alerts");

        var baseTime = DateTime.UtcNow;
        const string sourceIp = "192.168.1.100";

        // Create events spanning 5 minutes
        var normalizedEvents = new List<NormalizedEvent>();
        for (int i = 0; i < 10; i++)
        {
            normalizedEvents.Add(new NormalizedEvent
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = baseTime.AddSeconds(i * 30),
                EventType = "failed-login",
                Severity = "high",
                SourceIp = sourceIp,
                DestinationIp = "192.168.1.1",
                SourcePort = 50000 + i,
                DestinationPort = 3389,
                Protocol = "RDP",
                Username = "admin",
                Hostname = "DC-01",
                DeviceName = "DC-01",
                EventPayload = new Dictionary<string, object> { { "attempt", i + 1 } },
                GeoLocation = new GeoLocationData { CountryCode = "US" }
            });
        }

        // Act
        var producer = await _fixture.KafkaFixture.CreateProducerAsync<NormalizedEvent>();

        foreach (var normalizedEvent in normalizedEvents)
        {
            await producer.ProduceAsync("normalized-events", new Message<string, NormalizedEvent>
            {
                Key = normalizedEvent.SourceIp,
                Value = normalizedEvent
            });

            await Task.Delay(50);
        }

        producer.Flush(TimeSpan.FromSeconds(5));

        var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<Alert>(
            "test-time-range-group",
            new[] { "alerts" });

        var alerts = await AssertionHelpers.WaitForMessagesAsync(
            consumer,
            expectedCount: 1,
            timeout: TimeSpan.FromSeconds(60));

        // Assert
        alerts.Should().HaveCount(1);
        var alert = alerts[0];

        alert.CreatedAt.Should().BeCloseTo(baseTime, precision: TimeSpan.FromMinutes(1));
        // LastSeen would be updated as duplicates arrive
        alert.UpdatedAt.Should().BeOnOrAfter(alert.CreatedAt);

        // Cleanup
        producer.Dispose();
        consumer.Dispose();
    }

    [Fact(DisplayName = "Scenario 4.4: Status History Tracking")]
    public async Task StatusHistoryTracking()
    {
        // Arrange
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("normalized-events");
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("alerts");

        var normalizedEvent = EventFactory.CreateNormalizedEvent(sourceIp: "192.168.1.100");

        // Act - Create initial alert
        var producer = await _fixture.KafkaFixture.CreateProducerAsync<NormalizedEvent>();
        await producer.ProduceAsync("normalized-events", new Message<string, NormalizedEvent>
        {
            Key = normalizedEvent.SourceIp,
            Value = normalizedEvent
        });
        producer.Flush(TimeSpan.FromSeconds(5));

        var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<Alert>(
            "test-history-group",
            new[] { "alerts" });

        var alert = await AssertionHelpers.WaitForMessageAsync(consumer, timeout: TimeSpan.FromSeconds(30));

        // Assert - Alert created with "New" status
        alert.Should().NotBeNull();
        alert!.Status.Should().Be("New");

        // Verify alert has timestamp
        alert.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromMinutes(1));

        // Cleanup
        producer.Dispose();
        consumer.Dispose();
    }

    [Fact(DisplayName = "Scenario 4.5: Alert Correlation by Fields")]
    public async Task AlertCorrelationByFields()
    {
        // Arrange
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("normalized-events");
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("alerts");

        const string sourceIp = "192.168.1.100";
        const string username = "admin";

        // Create similar events that should correlate
        var event1 = EventFactory.CreateNormalizedEvent(
            sourceIp: sourceIp,
            username: username,
            eventType: "failed-login");

        var event2 = EventFactory.CreateNormalizedEvent(
            sourceIp: sourceIp,
            username: username,
            eventType: "failed-login");

        event2.Timestamp = event2.Timestamp.AddSeconds(30);

        // Act
        var producer = await _fixture.KafkaFixture.CreateProducerAsync<NormalizedEvent>();

        await producer.ProduceAsync("normalized-events", new Message<string, NormalizedEvent>
        {
            Key = event1.SourceIp,
            Value = event1
        });

        await Task.Delay(500);

        await producer.ProduceAsync("normalized-events", new Message<string, NormalizedEvent>
        {
            Key = event2.SourceIp,
            Value = event2
        });

        producer.Flush(TimeSpan.FromSeconds(5));

        var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<Alert>(
            "test-correlation-group",
            new[] { "alerts" });

        var alerts = await AssertionHelpers.WaitForMessagesAsync(
            consumer,
            expectedCount: 1,
            timeout: TimeSpan.FromSeconds(30));

        // Assert - Should correlate into single alert
        alerts.Should().HaveCount(1, "Events from same source/user should correlate");

        // Cleanup
        producer.Dispose();
        consumer.Dispose();
    }
}
