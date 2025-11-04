# Sakin Ingest Service - Testing Guide

## Build Status
✅ Main project builds successfully with only warnings (no errors)

## Core Functionality Implemented

### 1. Pipeline Architecture
- ✅ `IEventPipeline` interface for processing events
- ✅ `EventPipeline` implementation with priority-based processor execution
- ✅ Error handling and logging for failed processors

### 2. Event Sources
- ✅ `IEventSource` interface for consuming raw events
- ✅ `KafkaEventSource` implementation using Sakin.Messaging library
- ✅ Subscribes to `sakin.raw.events` topic
- ✅ Converts Kafka messages to `RawEvent` objects

### 3. Event Processors
- ✅ `IEventProcessor` interface for event transformation
- ✅ `PacketInspectorProcessor` for network sensor data
- ✅ Converts packet inspector JSON to `NetworkEvent` objects
- ✅ Protocol parsing (TCP, UDP, HTTP, HTTPS, TLS, etc.)
- ✅ Network-specific field mapping (SNI, HTTP details, etc.)

### 4. Event Sinks
- ✅ `IEventSink` interface for publishing normalized events
- ✅ `KafkaEventSink` implementation using Sakin.Messaging library
- ✅ Publishes to `sakin.normalized.events` topic
- ✅ JSON serialization with camelCase formatting

### 5. Main Service
- ✅ `IngestService` background service
- ✅ Orchestrates source → pipeline → sink flow
- ✅ Health check implementation
- ✅ Graceful shutdown handling
- ✅ Comprehensive logging and error handling

### 6. Configuration
- ✅ Kafka configuration (bootstrap servers, consumer/producer settings)
- ✅ Ingestion options (batch size, topics, retry settings)
- ✅ Environment-specific configuration files
- ✅ User secrets support

### 7. Docker Support
- ✅ Multi-stage Dockerfile for production builds
- ✅ Proper dependency management
- ✅ Configured for Docker Compose environment

## Configuration for Development

### Kafka Connection
- Bootstrap Servers: `localhost:29092` (Docker Compose external port)
- Input Topic: `sakin.raw.events`
- Output Topic: `sakin.normalized.events`
- Consumer Group: `sakin-ingest-group`

### Environment Variables
```bash
# Override configuration
export Kafka__BootstrapServers="localhost:29092"
export Ingestion__BatchSize="10"
export Logging__LogLevel__Sakin.Ingest="Debug"
```

## Running the Service

### Prerequisites
1. Docker Compose environment running (Kafka, PostgreSQL, etc.)
2. .NET 8.0 SDK

### Start Service
```bash
cd /home/engine/project/sakin-ingest
dotnet run --project Sakin.Ingest.csproj
```

### Expected Behavior
1. Service starts and connects to Kafka
2. Subscribes to `sakin.raw.events` topic
3. Logs show successful connection and subscription
4. Service remains running, ready to process events
5. Health check endpoint returns healthy status

## Testing Pipeline Flow

### Input Message Format (Raw Event)
```json
{
  "timestamp": "2024-01-01T12:00:00Z",
  "srcIp": "192.168.1.100",
  "dstIp": "10.0.0.1",
  "srcPort": 12345,
  "dstPort": 443,
  "protocol": "tcp",
  "bytesSent": 1024,
  "bytesReceived": 2048,
  "packetCount": 5,
  "sni": "example.com",
  "httpUrl": "/api/test",
  "httpMethod": "GET",
  "httpStatusCode": 200,
  "userAgent": "TestAgent/1.0"
}
```

### Output Message Format (Normalized Event)
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2024-01-01T12:00:00Z",
  "eventType": "networkTraffic",
  "severity": "info",
  "sourceIp": "192.168.1.100",
  "destinationIp": "10.0.0.1",
  "sourcePort": 12345,
  "destinationPort": 443,
  "protocol": "tcp",
  "bytesSent": 1024,
  "bytesReceived": 2048,
  "packetCount": 5,
  "sni": "example.com",
  "httpUrl": "/api/test",
  "httpMethod": "GET",
  "httpStatusCode": 200,
  "userAgent": "TestAgent/1.0",
  "metadata": {
    "original_source": "packet-inspector",
    "format": "JSON",
    "interface": "eth0"
  }
}
```

## Integration with Docker Compose

The service is configured to work with the existing Docker Compose environment:

1. **Kafka**: Uses `localhost:29092` for host connectivity
2. **Consumer Group**: `sakin-ingest-group` for load balancing
3. **Topics**: Auto-creation enabled in Kafka configuration
4. **Health Checks**: Ready for orchestration monitoring

## Acceptance Criteria Met

✅ **Service can subscribe to sensor output**
- KafkaEventSource subscribes to `sakin.raw.events` topic
- Uses Sakin.Messaging library for reliable consumption
- Configurable consumer group and batch processing

✅ **Produce normalized JSON to Kafka topic in dev compose**
- KafkaEventSink publishes to `sakin.normalized.events` topic
- Uses Sakin.Common models for consistent event schema
- JSON serialization with camelCase property names

✅ **Unit tests cover pipeline orchestration**
- EventPipeline tests for processor priority and error handling
- PacketInspectorProcessor tests for data transformation
- IngestService tests for service orchestration
- Mock-based testing for external dependencies

## Next Steps

1. **Deploy to Docker Compose**: Uncomment ingest service in docker-compose.dev.yml
2. **Test with Network Sensor**: Send test messages from sensor service
3. **Monitor Topics**: Use Kafka tools to verify message flow
4. **Add More Processors**: Implement processors for other data sources
5. **Performance Testing**: Load test with high-volume event streams