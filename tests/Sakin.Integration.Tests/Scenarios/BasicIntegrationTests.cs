using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Sakin.Common.Models;
using Sakin.Integration.Tests.Fixtures;
using Xunit;

namespace Sakin.Integration.Tests.Scenarios;

[Collection("Integration Tests")]
public class BasicIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public BasicIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Infrastructure: PostgreSQL connection successful")]
    public async Task PostgresConnectionSuccessful()
    {
        // Arrange
        var connection = await _fixture.PostgresFixture.GetConnectionAsync();

        // Act
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1";
        var result = await cmd.ExecuteScalarAsync();

        // Assert
        result.Should().Be(1);
    }

    [Fact(DisplayName = "Infrastructure: Redis connection successful")]
    public async Task RedisConnectionSuccessful()
    {
        // Arrange & Act
        var db = _fixture.RedisFixture.Connection.GetDatabase();
        await db.StringSetAsync("test-key", "test-value");
        var value = await db.StringGetAsync("test-key");

        // Assert
        value.ToString().Should().Be("test-value");
    }

    [Fact(DisplayName = "Infrastructure: Kafka topics created")]
    public async Task KafkaTopicsCreated()
    {
        // Arrange & Act
        await _fixture.KafkaFixture.EnsureTopicsExistAsync();

        // Assert - No exception thrown means topics exist or were created
        // This passes if KafkaFixture initialized successfully
        _fixture.KafkaBootstrapServers.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Kafka: Produce and Consume Message")]
    public async Task KafkaProduceAndConsumeMessage()
    {
        // Arrange
        await _fixture.KafkaFixture.EnsureTopicExistsAsync("test-topic");

        var testEvent = new NormalizedEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = EventType.SecurityEvent,
            Severity = Severity.Medium,
            SourceIp = "192.168.1.100",
            DestinationIp = "192.168.1.1",
            SourcePort = 12345,
            DestinationPort = 443,
            Protocol = Protocol.TCP,
            Payload = "test payload",
            Metadata = new Dictionary<string, object> { { "test", "data" } },
            DeviceName = "TEST-DEVICE",
            Username = "testuser",
            Hostname = "test-host"
        };

        // Act - Produce message
        var producer = await _fixture.KafkaFixture.CreateProducerAsync<NormalizedEvent>();
        var deliveryResult = await producer.ProduceAsync(
            "test-topic",
            new Confluent.Kafka.Message<string, NormalizedEvent>
            {
                Key = testEvent.SourceIp,
                Value = testEvent
            });

        producer.Flush(TimeSpan.FromSeconds(5));

        // Act - Consume message
        var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<NormalizedEvent>(
            "test-group",
            new[] { "test-topic" });

        NormalizedEvent? receivedEvent = null;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
        {
            var message = consumer.Consume(TimeSpan.FromSeconds(1));
            if (message?.Message.Value != null)
            {
                receivedEvent = message.Message.Value;
                consumer.Commit(message);
                break;
            }
        }

        // Assert
        receivedEvent.Should().NotBeNull("Message should be produced and consumed");
        receivedEvent!.SourceIp.Should().Be(testEvent.SourceIp);
        receivedEvent.EventType.Should().Be(testEvent.EventType);
        receivedEvent.Timestamp.Should().BeCloseTo(testEvent.Timestamp, TimeSpan.FromSeconds(1));

        // Cleanup
        producer.Dispose();
        consumer.Dispose();
    }

    [Fact(DisplayName = "Database: Alert table exists and accessible")]
    public async Task DatabaseAlertTableAccessible()
    {
        // Arrange
        var connection = await _fixture.PostgresFixture.GetConnectionAsync();

        // Act
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT table_name 
            FROM information_schema.tables 
            WHERE table_schema = 'public' AND table_name = 'alerts'
            LIMIT 1
        ";
        var result = await cmd.ExecuteScalarAsync();

        // Assert
        result.Should().NotBeNull("Alerts table should exist after migrations");
    }

    [Fact(DisplayName = "Redis: Pub/Sub Channel")]
    public async Task RedisChannelSubscription()
    {
        // Arrange
        var connection = _fixture.RedisFixture.Connection;
        var pubsub = connection.GetSubscriber();
        var received = false;
        var testMessage = "test-message";

        // Act
        await pubsub.SubscribeAsync("test-channel", (channel, value) =>
        {
            if (value == testMessage)
            {
                received = true;
            }
        });

        await Task.Delay(100);
        await pubsub.PublishAsync("test-channel", testMessage);
        await Task.Delay(100);

        // Assert
        received.Should().BeTrue("Message should be received via Redis pub/sub");
    }

    [Fact(DisplayName = "Data: NormalizedEvent Serialization")]
    public async Task NormalizedEventSerialization()
    {
        // Arrange
        var originalEvent = new NormalizedEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = EventType.FailedLogin,
            Severity = Severity.High,
            SourceIp = "10.0.0.1",
            DestinationIp = "10.0.0.2",
            SourcePort = 54321,
            DestinationPort = 22,
            Protocol = Protocol.TCP,
            Payload = "test",
            Metadata = new Dictionary<string, object> { { "key", "value" } }
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(originalEvent);
        var deserializedEvent = System.Text.Json.JsonSerializer.Deserialize<NormalizedEvent>(json);

        // Assert
        deserializedEvent.Should().NotBeNull();
        deserializedEvent!.Id.Should().Be(originalEvent.Id);
        deserializedEvent.SourceIp.Should().Be(originalEvent.SourceIp);
        deserializedEvent.EventType.Should().Be(originalEvent.EventType);
        deserializedEvent.Severity.Should().Be(originalEvent.Severity);
    }
}
