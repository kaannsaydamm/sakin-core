# Sakin.Messaging

Kafka messaging library for the SAKIN security platform. Provides high-level abstractions for producing and consuming messages with built-in retry policies, batching, and error handling.

## Features

- **Producer**: High-throughput message producer with batching, compression, and idempotence
- **Consumer**: Reliable message consumer with automatic offset management and retry policies
- **Serialization**: JSON serialization with camelCase naming and enum support
- **Configuration**: Flexible configuration using IOptions pattern
- **Logging**: Comprehensive structured logging throughout
- **Error Handling**: Custom exceptions with detailed context
- **Retry Policies**: Built-in retry logic using Polly

## Installation

Add a project reference to `Sakin.Messaging`:

```xml
<ItemGroup>
  <ProjectReference Include="..\sakin-utils\Sakin.Messaging\Sakin.Messaging.csproj" />
</ItemGroup>
```

## Configuration

### appsettings.json

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "ClientId": "sakin-client",
    "RequestTimeoutMs": 30000,
    "MessageTimeoutMs": 60000,
    "SecurityProtocol": "Plaintext"
  },
  "KafkaProducer": {
    "DefaultTopic": "events",
    "BatchSize": 100,
    "LingerMs": 10,
    "CompressionType": "Snappy",
    "RequiredAcks": "Leader",
    "RetryCount": 3,
    "RetryBackoffMs": 100,
    "EnableIdempotence": true,
    "MaxInFlight": 5
  },
  "KafkaConsumer": {
    "GroupId": "sakin-consumer-group",
    "Topics": ["events", "alerts"],
    "AutoOffsetReset": "Earliest",
    "EnableAutoCommit": false,
    "AutoCommitIntervalMs": 5000,
    "SessionTimeoutMs": 10000,
    "MaxPollIntervalMs": 300000,
    "MaxRetries": 3,
    "RetryDelayMs": 1000
  }
}
```

### Dependency Injection

```csharp
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Serialization;

// Configure services
services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
services.Configure<ProducerOptions>(configuration.GetSection(ProducerOptions.SectionName));
services.Configure<ConsumerOptions>(configuration.GetSection(ConsumerOptions.SectionName));

// Register serializer
services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();

// Register producer and consumer
services.AddSingleton<IKafkaProducer, KafkaProducer>();
services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
```

## Usage

### Producing Messages

```csharp
public class EventPublisher
{
    private readonly IKafkaProducer _producer;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(IKafkaProducer producer, ILogger<EventPublisher> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    public async Task PublishEventAsync(NetworkEvent networkEvent)
    {
        try
        {
            var result = await _producer.ProduceAsync("events", networkEvent, networkEvent.Id);
            _logger.LogInformation("Event published to partition {Partition}, offset {Offset}",
                result.Partition, result.Offset);
        }
        catch (KafkaProducerException ex)
        {
            _logger.LogError(ex, "Failed to publish event to topic {Topic}", ex.Topic);
            throw;
        }
    }

    public async Task PublishBatchAsync(IEnumerable<NetworkEvent> events)
    {
        foreach (var evt in events)
        {
            await _producer.ProduceAsync("events", evt, evt.Id);
        }

        await _producer.FlushAsync();
    }
}
```

### Consuming Messages

```csharp
public class EventConsumerService : BackgroundService
{
    private readonly IKafkaConsumer _consumer;
    private readonly ILogger<EventConsumerService> _logger;

    public EventConsumerService(IKafkaConsumer consumer, ILogger<EventConsumerService> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe("events", "alerts");

        await _consumer.ConsumeAsync<NetworkEvent>(async result =>
        {
            _logger.LogInformation("Processing event from topic {Topic}, offset {Offset}",
                result.Topic, result.Offset);

            if (result.Message != null)
            {
                // Process the message
                await ProcessEventAsync(result.Message);
            }
        }, stoppingToken);
    }

    private async Task ProcessEventAsync(NetworkEvent evt)
    {
        // Your processing logic here
        await Task.CompletedTask;
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}
```

## Configuration Options

### KafkaOptions

- `BootstrapServers`: Kafka broker addresses (default: "localhost:9092")
- `ClientId`: Client identifier (default: "sakin-client")
- `RequestTimeoutMs`: Request timeout in milliseconds (default: 30000)
- `MessageTimeoutMs`: Message timeout in milliseconds (default: 60000)
- `SecurityProtocol`: Security protocol (Plaintext, Ssl, SaslPlaintext, SaslSsl)
- `SaslMechanism`: SASL mechanism when using SASL security
- `SaslUsername`: Username for SASL authentication
- `SaslPassword`: Password for SASL authentication

### ProducerOptions

- `DefaultTopic`: Default topic for messages (default: "events")
- `BatchSize`: Number of bytes to batch before sending (default: 100)
- `LingerMs`: Time to wait before sending batch (default: 10)
- `CompressionType`: Compression algorithm (None, Gzip, Snappy, Lz4, Zstd)
- `RequiredAcks`: Acknowledgment level (None, Leader, All)
- `RetryCount`: Number of retry attempts (default: 3)
- `RetryBackoffMs`: Backoff time between retries (default: 100)
- `EnableIdempotence`: Enable idempotent producer (default: true)
- `MaxInFlight`: Maximum in-flight requests (default: 5)

### ConsumerOptions

- `GroupId`: Consumer group identifier (default: "sakin-consumer-group")
- `Topics`: Array of topics to subscribe to
- `AutoOffsetReset`: Offset reset behavior (Earliest, Latest, Error)
- `EnableAutoCommit`: Enable automatic offset commits (default: false)
- `AutoCommitIntervalMs`: Auto-commit interval (default: 5000)
- `SessionTimeoutMs`: Session timeout (default: 10000)
- `MaxPollIntervalMs`: Maximum poll interval (default: 300000)
- `MaxRetries`: Maximum retry attempts (default: 3)
- `RetryDelayMs`: Delay between retries (default: 1000)

## Error Handling

The library provides custom exceptions for better error handling:

- `KafkaProducerException`: Thrown when producer operations fail, includes topic and key context
- `KafkaConsumerException`: Thrown when consumer operations fail, includes topic, partition, and offset context

## Testing

The library includes comprehensive unit and integration tests:

- Unit tests for configuration, serialization, and basic operations
- Integration tests using Testcontainers.Kafka for end-to-end testing

Run tests:
```bash
dotnet test sakin-utils/Sakin.Messaging.Tests/
```

## Dependencies

- Confluent.Kafka 2.6.1
- Polly 8.5.0 (retry policies)
- Microsoft.Extensions.Logging.Abstractions 9.0.10
- Microsoft.Extensions.Options 9.0.10

## License

See LICENSE file in the repository root.
