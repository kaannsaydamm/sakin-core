# Sakin.Messaging Implementation Summary

## Overview

Sakin.Messaging is a comprehensive Kafka messaging library that provides high-level abstractions for producing and consuming messages in the SAKIN security platform. The library follows established patterns from Sakin.Common and integrates seamlessly with .NET dependency injection.

## Implementation Details

### Project Structure

```
Sakin.Messaging/
├── Configuration/
│   ├── KafkaOptions.cs           # Core Kafka connection settings
│   ├── ProducerOptions.cs        # Producer-specific configuration
│   └── ConsumerOptions.cs        # Consumer-specific configuration
├── Producer/
│   ├── IKafkaProducer.cs         # Producer interface
│   └── KafkaProducer.cs          # Producer implementation with retry logic
├── Consumer/
│   ├── IKafkaConsumer.cs         # Consumer interface
│   └── KafkaConsumer.cs          # Consumer implementation with offset management
├── Serialization/
│   ├── IMessageSerializer.cs     # Serialization interface
│   └── JsonMessageSerializer.cs  # JSON serialization with camelCase support
├── Exceptions/
│   ├── KafkaProducerException.cs # Producer-specific exceptions
│   └── KafkaConsumerException.cs # Consumer-specific exceptions
├── README.md                     # Library documentation
├── EXAMPLES.md                   # Usage examples
└── appsettings.sample.json       # Sample configuration

Sakin.Messaging.Tests/
├── Configuration/
│   └── KafkaOptionsTests.cs      # Configuration unit tests
├── Producer/
│   └── KafkaProducerIntegrationTests.cs  # Producer integration tests
├── Consumer/
│   └── KafkaConsumerIntegrationTests.cs  # Consumer integration tests
└── Serialization/
    └── JsonMessageSerializerTests.cs     # Serialization unit tests
```

## Key Features Implemented

### 1. Configuration Management

Following the IOptions pattern from Sakin.Common:

- **KafkaOptions**: Bootstrap servers, client ID, timeouts, security settings
- **ProducerOptions**: Batching, compression, acknowledgments, retry policies
- **ConsumerOptions**: Group ID, topics, offset management, polling configuration

All configuration supports:
- appsettings.json
- Environment variables (production)
- User secrets (development)

### 2. Producer Implementation

**Features:**
- Asynchronous message production with `ProduceAsync`
- Configurable batching (BatchSize, LingerMs)
- Multiple compression types (None, Gzip, Snappy, Lz4, Zstd)
- Acknowledgment levels (None, Leader, All)
- Exponential backoff retry policy using Polly
- Idempotence support
- Manual and automatic flushing
- Comprehensive error handling and logging

**Key Methods:**
```csharp
Task<MessageResult> ProduceAsync<T>(string topic, T message, string? key = null, CancellationToken cancellationToken = default);
Task<MessageResult> ProduceAsync<T>(T message, string? key = null, CancellationToken cancellationToken = default);
void Flush(TimeSpan timeout);
Task FlushAsync(CancellationToken cancellationToken = default);
```

### 3. Consumer Implementation

**Features:**
- Asynchronous message consumption with `ConsumeAsync`
- Configurable offset management (auto-commit or manual)
- Multiple auto-offset reset strategies (Earliest, Latest, Error)
- Retry policy for message processing failures
- Dynamic topic subscription/unsubscription
- Manual commit support
- Graceful shutdown handling
- Comprehensive error handling and logging

**Key Methods:**
```csharp
Task ConsumeAsync<T>(Func<ConsumeResult<T>, Task> messageHandler, CancellationToken cancellationToken);
void Commit();
void Subscribe(params string[] topics);
void Unsubscribe();
```

### 4. Serialization

**JsonMessageSerializer:**
- Automatic camelCase property naming
- Enum serialization to camelCase strings
- UTF-8 encoding
- Null value handling
- Comprehensive error messages

Compatible with SAKIN event schema (NormalizedEvent, NetworkEvent from Sakin.Common).

### 5. Error Handling

**Custom Exceptions:**
- **KafkaProducerException**: Includes topic and key context
- **KafkaConsumerException**: Includes topic, partition, and offset context

Both exceptions support inner exceptions for proper error chaining.

### 6. Logging

Comprehensive structured logging throughout:
- Connection establishment/disposal
- Message production/consumption
- Retry attempts with backoff times
- Error conditions with full context
- Kafka client internal logs mapped to appropriate log levels

### 7. Retry Policies

**Producer:**
- Configurable retry count (default: 3)
- Exponential backoff (RetryBackoffMs * 2^(retryAttempt-1))
- Detailed retry logging

**Consumer:**
- Configurable max retries for message processing (default: 3)
- Linear backoff (RetryDelayMs * retryAttempt)
- Continues consuming after retry exhaustion (logs error)

## Testing Strategy

### Unit Tests (15 tests)

**Configuration Tests:**
- Default value validation
- Property setter validation
- Enum value coverage

**Serialization Tests:**
- Valid object serialization
- Deserialization round-trip
- Null/empty input handling
- CamelCase formatting
- Enum serialization
- Complex object handling

### Integration Tests (using Testcontainers)

**Producer Tests:**
- Single message production
- Batch message production
- Default topic usage
- Flush operations

**Consumer Tests:**
- Message consumption
- Manual commit
- Topic subscription/unsubscription
- Multi-message handling

All integration tests use real Kafka instances via Testcontainers for accurate testing.

## Dependencies

```xml
<PackageReference Include="Confluent.Kafka" Version="2.6.1" />
<PackageReference Include="Polly" Version="8.5.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.10" />
<PackageReference Include="Microsoft.Extensions.Options" Version="9.0.10" />
```

**Test Dependencies:**
```xml
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Testcontainers.Kafka" Version="3.10.0" />
```

## Integration with SAKIN Platform

### With Network Sensor
The network sensor can use Sakin.Messaging to publish network events to Kafka:

```csharp
services.AddSingleton<IKafkaProducer, KafkaProducer>();

// In PackageInspector
await _producer.ProduceAsync("network-events", networkEvent, eventId);
```

### With Ingest Service
The ingest service can consume events from Kafka and store them:

```csharp
services.AddSingleton<IKafkaConsumer, KafkaConsumer>();

// In background service
await _consumer.ConsumeAsync<NetworkEvent>(async result =>
{
    await _eventStore.SaveAsync(result.Message);
    await _opensearchClient.IndexAsync(result.Message);
}, cancellationToken);
```

### With Correlation Service
The correlation service can consume events, correlate them, and produce alerts:

```csharp
services.AddSingleton<IKafkaProducer, KafkaProducer>();
services.AddSingleton<IKafkaConsumer, KafkaConsumer>();

// Consume events
await _consumer.ConsumeAsync<NetworkEvent>(async result =>
{
    var alert = await _correlationEngine.AnalyzeAsync(result.Message);
    if (alert != null)
    {
        await _producer.ProduceAsync("security-alerts", alert);
    }
}, cancellationToken);
```

## Configuration in Dev Environment

The existing docker-compose.dev.yml already includes Kafka and Zookeeper:

```yaml
kafka:
  image: confluentinc/cp-kafka:7.5.0
  ports:
    - "9092:9092"      # Internal
    - "29092:29092"    # External (localhost)
```

Services should connect to:
- From host: `localhost:29092`
- From Docker: `kafka:9092`

## Performance Considerations

### Producer Optimizations
- **Batching**: Messages are batched up to BatchSize (default: 100 bytes)
- **Linger**: Wait up to LingerMs (default: 10ms) before sending batch
- **Compression**: Snappy compression reduces network I/O (default)
- **Max In-Flight**: Up to 5 requests can be in-flight simultaneously
- **Idempotence**: Prevents duplicate messages without performance penalty

### Consumer Optimizations
- **Fetch Min Bytes**: Fetch at least 1 byte per request (configurable)
- **Session Timeout**: 10-second session timeout (configurable)
- **Max Poll Interval**: 5-minute max poll interval (configurable)
- **Manual Commit**: Disabled auto-commit for better control and at-least-once delivery

## Security Support

The library supports all Kafka security protocols:
- **Plaintext**: No encryption (development)
- **SSL**: TLS encryption
- **SASL_PLAINTEXT**: SASL authentication without encryption
- **SASL_SSL**: SASL authentication with TLS encryption

SASL mechanisms supported: PLAIN, SCRAM-SHA-256, SCRAM-SHA-512, GSSAPI

## Error Scenarios Handled

1. **Network Failures**: Retry with exponential backoff
2. **Broker Unavailable**: Producer/consumer will retry
3. **Serialization Errors**: Detailed exception with type information
4. **Message Too Large**: Error logged with context
5. **Timeout**: Configurable timeouts with proper exception
6. **Consumer Group Rebalance**: Handled automatically by Confluent.Kafka
7. **Offset Commit Failure**: Logged and can be retried
8. **Cancellation**: Graceful shutdown on cancellation token

## Monitoring and Observability

### Logging
All operations log at appropriate levels:
- **Trace/Debug**: Message-level details in development
- **Information**: Connection establishment, message delivery, offsets
- **Warning**: Retry attempts, recoverable errors
- **Error**: Unrecoverable errors with full context

### Metrics (Future Enhancement)
Recommended metrics to track:
- Messages produced/consumed per second
- Producer/consumer lag
- Error rates
- Retry counts
- Latency percentiles (p50, p95, p99)

Can be implemented using libraries like:
- prometheus-net
- OpenTelemetry
- Application Insights

## Best Practices Implemented

1. **Dispose Pattern**: Both producer and consumer implement IDisposable properly
2. **Async/Await**: All I/O operations are asynchronous
3. **Cancellation Support**: All async methods accept CancellationToken
4. **Structured Logging**: All logs include relevant context
5. **Configuration Validation**: Validates configuration on startup
6. **Immutable Results**: MessageResult and ConsumeResult use record types
7. **Interface Segregation**: Separate interfaces for producer and consumer
8. **Dependency Injection**: Full support for Microsoft.Extensions.DependencyInjection

## Testing in Dev Environment

### Start Kafka
```bash
cd deployments
docker-compose -f docker-compose.dev.yml up kafka zookeeper -d
```

### Run Unit Tests (Fast)
```bash
dotnet test sakin-utils/Sakin.Messaging.Tests/ --filter "FullyQualifiedName~Configuration|FullyQualifiedName~Serialization"
```

### Run Integration Tests (Requires Docker)
```bash
dotnet test sakin-utils/Sakin.Messaging.Tests/
```

### Test with Sample Application
See EXAMPLES.md for complete working examples.

## Future Enhancements

1. **Dead Letter Queue**: Automatic DLQ for failed messages
2. **Metrics**: Built-in metrics using OpenTelemetry
3. **Schema Registry**: Support for Avro/Protobuf with Confluent Schema Registry
4. **Transactional Producer**: Support for exactly-once semantics
5. **Consumer Lag Monitoring**: Built-in consumer lag tracking
6. **Health Checks**: IHealthCheck implementation for ASP.NET Core
7. **Rate Limiting**: Built-in rate limiting for producer
8. **Circuit Breaker**: Circuit breaker pattern for resilience

## Acceptance Criteria Met

✅ **Kafka client abstraction**: IKafkaProducer and IKafkaConsumer interfaces with full implementations

✅ **Producer/consumer wrappers**: Using Confluent.Kafka with comprehensive wrappers

✅ **Configurable topics**: Topics configurable via IOptions pattern

✅ **Batching**: BatchSize and LingerMs configurable

✅ **Retry policies**: Exponential backoff for producer, linear for consumer using Polly

✅ **Integration tests**: Using Testcontainers.Kafka for real Kafka testing

✅ **Unit tests**: 15+ unit tests covering configuration, serialization

✅ **Serialization coverage**: Tests for JSON serialization with camelCase, enums, complex objects

✅ **Error logging**: Comprehensive structured logging with context throughout

✅ **Dev environment**: Works with existing docker-compose.dev.yml Kafka setup

## Conclusion

Sakin.Messaging provides a production-ready Kafka messaging library that follows SAKIN platform conventions and patterns. It offers a balance of simplicity for basic use cases and flexibility for advanced scenarios, with comprehensive error handling, logging, and testing.
