# Sakin.Messaging Examples

This document provides practical examples for using the Sakin.Messaging library.

## Table of Contents

- [Setup](#setup)
- [Producer Examples](#producer-examples)
- [Consumer Examples](#consumer-examples)
- [Advanced Scenarios](#advanced-scenarios)

## Setup

### 1. Add Package Reference

```xml
<ItemGroup>
  <ProjectReference Include="..\sakin-utils\Sakin.Messaging\Sakin.Messaging.csproj" />
</ItemGroup>
```

### 2. Configure Services (Program.cs)

```csharp
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Serialization;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Configure Kafka options
        services.Configure<KafkaOptions>(
            context.Configuration.GetSection(KafkaOptions.SectionName));
        services.Configure<ProducerOptions>(
            context.Configuration.GetSection(ProducerOptions.SectionName));
        services.Configure<ConsumerOptions>(
            context.Configuration.GetSection(ConsumerOptions.SectionName));

        // Register serializer
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();

        // Register producer and consumer
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();

        // Register your services
        services.AddHostedService<EventPublisherService>();
        services.AddHostedService<EventConsumerService>();
    });

await builder.Build().RunAsync();
```

## Producer Examples

### Example 1: Simple Message Publishing

```csharp
public class EventPublisherService : BackgroundService
{
    private readonly IKafkaProducer _producer;
    private readonly ILogger<EventPublisherService> _logger;

    public EventPublisherService(
        IKafkaProducer producer,
        ILogger<EventPublisherService> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var networkEvent = new NetworkEvent
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                EventType = EventType.NetworkTraffic,
                Severity = Severity.Info,
                SourceIp = "192.168.1.100",
                DestinationIp = "8.8.8.8",
                Protocol = Protocol.Https
            };

            // Publish to specific topic
            var result = await _producer.ProduceAsync(
                "network-events",
                networkEvent,
                networkEvent.Id,
                stoppingToken);

            _logger.LogInformation(
                "Published event to partition {Partition}, offset {Offset}",
                result.Partition, result.Offset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event");
        }

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override void Dispose()
    {
        _producer?.Dispose();
        base.Dispose();
    }
}
```

### Example 2: Batch Publishing with Flush

```csharp
public class BatchEventPublisher
{
    private readonly IKafkaProducer _producer;
    private readonly ILogger<BatchEventPublisher> _logger;

    public BatchEventPublisher(
        IKafkaProducer producer,
        ILogger<BatchEventPublisher> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    public async Task PublishBatchAsync(
        IEnumerable<NetworkEvent> events,
        CancellationToken cancellationToken = default)
    {
        var successCount = 0;
        var failureCount = 0;

        foreach (var evt in events)
        {
            try
            {
                await _producer.ProduceAsync(
                    "network-events",
                    evt,
                    evt.Id,
                    cancellationToken);
                
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish event {EventId}", evt.Id);
                failureCount++;
            }
        }

        // Ensure all messages are sent
        await _producer.FlushAsync(cancellationToken);

        _logger.LogInformation(
            "Batch publish completed: {SuccessCount} succeeded, {FailureCount} failed",
            successCount, failureCount);
    }
}
```

### Example 3: Using Default Topic

```csharp
public class SimpleEventPublisher
{
    private readonly IKafkaProducer _producer;

    public SimpleEventPublisher(IKafkaProducer producer)
    {
        _producer = producer;
    }

    public async Task PublishAsync(NetworkEvent evt)
    {
        // Uses default topic from configuration
        var result = await _producer.ProduceAsync(evt, evt.Id);
        
        // result.Topic will be the default topic
        Console.WriteLine($"Published to {result.Topic}");
    }
}
```

## Consumer Examples

### Example 4: Basic Consumer Service

```csharp
public class EventConsumerService : BackgroundService
{
    private readonly IKafkaConsumer _consumer;
    private readonly ILogger<EventConsumerService> _logger;

    public EventConsumerService(
        IKafkaConsumer consumer,
        ILogger<EventConsumerService> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting event consumer");

        // Subscribe to topics (if not already configured)
        _consumer.Subscribe("network-events", "security-alerts");

        await _consumer.ConsumeAsync<NetworkEvent>(async result =>
        {
            _logger.LogInformation(
                "Received event {EventId} from topic {Topic}, partition {Partition}, offset {Offset}",
                result.Message?.Id, result.Topic, result.Partition, result.Offset);

            if (result.Message != null)
            {
                await ProcessEventAsync(result.Message);
            }
        }, stoppingToken);

        _logger.LogInformation("Event consumer stopped");
    }

    private async Task ProcessEventAsync(NetworkEvent evt)
    {
        // Your business logic here
        _logger.LogInformation("Processing event: {EventType} from {SourceIp}",
            evt.EventType, evt.SourceIp);
        
        await Task.CompletedTask;
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}
```

### Example 5: Consumer with Error Handling

```csharp
public class ResilientEventConsumer : BackgroundService
{
    private readonly IKafkaConsumer _consumer;
    private readonly ILogger<ResilientEventConsumer> _logger;

    public ResilientEventConsumer(
        IKafkaConsumer consumer,
        ILogger<ResilientEventConsumer> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe("critical-events");

        await _consumer.ConsumeAsync<NetworkEvent>(async result =>
        {
            try
            {
                if (result.Message == null)
                {
                    _logger.LogWarning("Received null message at offset {Offset}", result.Offset);
                    return;
                }

                await ProcessWithRetryAsync(result.Message);

                _logger.LogInformation("Successfully processed event {EventId}", result.Message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process event from topic {Topic}, partition {Partition}, offset {Offset}",
                    result.Topic, result.Partition, result.Offset);

                // Send to dead letter queue or handle appropriately
                await SendToDeadLetterQueueAsync(result.Message, ex);
            }
        }, stoppingToken);
    }

    private async Task ProcessWithRetryAsync(NetworkEvent evt)
    {
        // Your processing logic with retries
        await Task.CompletedTask;
    }

    private async Task SendToDeadLetterQueueAsync(NetworkEvent? evt, Exception ex)
    {
        // Dead letter queue logic
        await Task.CompletedTask;
    }
}
```

### Example 6: Multi-Topic Consumer with Routing

```csharp
public class MultiTopicConsumer : BackgroundService
{
    private readonly IKafkaConsumer _consumer;
    private readonly ILogger<MultiTopicConsumer> _logger;

    public MultiTopicConsumer(
        IKafkaConsumer consumer,
        ILogger<MultiTopicConsumer> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe("network-events", "security-alerts", "system-logs");

        await _consumer.ConsumeAsync<NetworkEvent>(async result =>
        {
            switch (result.Topic)
            {
                case "network-events":
                    await HandleNetworkEventAsync(result.Message);
                    break;
                case "security-alerts":
                    await HandleSecurityAlertAsync(result.Message);
                    break;
                case "system-logs":
                    await HandleSystemLogAsync(result.Message);
                    break;
                default:
                    _logger.LogWarning("Unknown topic: {Topic}", result.Topic);
                    break;
            }
        }, stoppingToken);
    }

    private async Task HandleNetworkEventAsync(NetworkEvent? evt)
    {
        await Task.CompletedTask;
    }

    private async Task HandleSecurityAlertAsync(NetworkEvent? evt)
    {
        await Task.CompletedTask;
    }

    private async Task HandleSystemLogAsync(NetworkEvent? evt)
    {
        await Task.CompletedTask;
    }
}
```

## Advanced Scenarios

### Example 7: Custom Serializer

```csharp
using System.Text;
using System.Text.Json;
using Sakin.Messaging.Serialization;

public class CustomMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    public CustomMessageSerializer()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };
    }

    public byte[] Serialize<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, _options);
        return Encoding.UTF8.GetBytes(json);
    }

    public T? Deserialize<T>(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<T>(json, _options);
    }
}

// Register in DI
services.AddSingleton<IMessageSerializer, CustomMessageSerializer>();
```

### Example 8: Manual Offset Commit

```csharp
public class ManualCommitConsumer : BackgroundService
{
    private readonly IKafkaConsumer _consumer;
    private readonly ILogger<ManualCommitConsumer> _logger;
    private int _messageCount = 0;
    private const int CommitInterval = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe("events");

        await _consumer.ConsumeAsync<NetworkEvent>(async result =>
        {
            await ProcessEventAsync(result.Message);
            
            _messageCount++;

            // Commit every 10 messages
            if (_messageCount % CommitInterval == 0)
            {
                _consumer.Commit();
                _logger.LogInformation("Committed offsets after {Count} messages", _messageCount);
            }
        }, stoppingToken);

        // Final commit on shutdown
        _consumer.Commit();
    }

    private async Task ProcessEventAsync(NetworkEvent? evt)
    {
        await Task.CompletedTask;
    }
}
```

### Example 9: Dynamic Topic Subscription

```csharp
public class DynamicTopicConsumer
{
    private readonly IKafkaConsumer _consumer;
    private readonly ILogger<DynamicTopicConsumer> _logger;

    public DynamicTopicConsumer(
        IKafkaConsumer consumer,
        ILogger<DynamicTopicConsumer> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    public void UpdateSubscription(params string[] topics)
    {
        _logger.LogInformation("Updating subscription to: {Topics}",
            string.Join(", ", topics));
        
        _consumer.Unsubscribe();
        _consumer.Subscribe(topics);
    }

    public async Task StartConsumingAsync(CancellationToken cancellationToken)
    {
        await _consumer.ConsumeAsync<NetworkEvent>(async result =>
        {
            _logger.LogInformation("Consumed from {Topic}: {EventId}",
                result.Topic, result.Message?.Id);
            
            await Task.CompletedTask;
        }, cancellationToken);
    }
}
```

### Example 10: Integration with Event Store

```csharp
public class EventStorePublisher
{
    private readonly IKafkaProducer _producer;
    private readonly IEventStore _eventStore;
    private readonly ILogger<EventStorePublisher> _logger;

    public EventStorePublisher(
        IKafkaProducer producer,
        IEventStore eventStore,
        ILogger<EventStorePublisher> logger)
    {
        _producer = producer;
        _eventStore = eventStore;
        _logger = logger;
    }

    public async Task PublishAndStoreAsync(NetworkEvent evt)
    {
        try
        {
            // Store in database first
            await _eventStore.SaveAsync(evt);

            // Then publish to Kafka
            var result = await _producer.ProduceAsync("events", evt, evt.Id);

            _logger.LogInformation(
                "Event {EventId} stored and published to offset {Offset}",
                evt.Id, result.Offset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish and store event {EventId}", evt.Id);
            
            // Compensate: try to remove from store if Kafka publish failed
            await _eventStore.DeleteAsync(evt.Id);
            
            throw;
        }
    }
}

public interface IEventStore
{
    Task SaveAsync(NetworkEvent evt);
    Task DeleteAsync(string eventId);
}
```

## Running with Docker Compose

To test these examples locally with Kafka running in Docker:

```bash
# Start Kafka and other services
cd deployments
docker-compose -f docker-compose.dev.yml up kafka zookeeper -d

# Wait for Kafka to be ready
docker-compose -f docker-compose.dev.yml ps

# Run your application
cd ../your-service
dotnet run
```

## Configuration Examples

### Development (appsettings.Development.json)

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:29092"
  },
  "Logging": {
    "LogLevel": {
      "Sakin.Messaging": "Debug"
    }
  }
}
```

### Production (Environment Variables)

```bash
export Kafka__BootstrapServers="kafka-broker-1:9092,kafka-broker-2:9092"
export Kafka__SecurityProtocol="SaslSsl"
export Kafka__SaslMechanism="PLAIN"
export Kafka__SaslUsername="your-username"
export Kafka__SaslPassword="your-password"
export KafkaProducer__EnableIdempotence="true"
export KafkaProducer__RequiredAcks="All"
```

## Troubleshooting

### Issue: Consumer not receiving messages

**Solution**: Check that:
1. Kafka is running and accessible
2. Topics exist and have messages
3. Consumer group ID is correct
4. AutoOffsetReset is set appropriately (Earliest/Latest)

### Issue: Producer timeout errors

**Solution**: 
1. Increase `MessageTimeoutMs` and `RequestTimeoutMs`
2. Check network connectivity to Kafka brokers
3. Verify Kafka broker configuration

### Issue: Messages not serializing correctly

**Solution**: 
1. Ensure your model classes are properly serializable
2. Check JsonSerializerOptions configuration
3. Verify enum values match expected format (camelCase)

## Best Practices

1. **Always dispose producers and consumers** - Use `using` statements or implement IDisposable
2. **Handle cancellation tokens properly** - Ensure graceful shutdown
3. **Use structured logging** - Include relevant context (topic, partition, offset)
4. **Configure appropriate retry policies** - Balance between reliability and performance
5. **Monitor consumer lag** - Set up monitoring for consumer group lag
6. **Use batching for high throughput** - Configure BatchSize and LingerMs appropriately
7. **Enable idempotence for producers** - Prevents duplicate messages
8. **Test with Testcontainers** - Use integration tests with real Kafka instances
