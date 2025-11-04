# Sakin Ingest

## Overview
Data ingestion and normalization layer for processing security events from multiple sources.

## Purpose
This service acts as the central ingestion pipeline that:
- Receives raw security data from collectors and sensors via Kafka
- Normalizes data into standardized formats using configurable pipeline processors
- Validates and enriches incoming events
- Routes processed data to appropriate downstream services via Kafka
- Handles data buffering and backpressure

## Status
✅ **Implemented** - Worker service with pipeline architecture and Kafka integration.

## Features
- High-throughput event processing with configurable batching
- Configurable pipeline architecture with multiple processors
- Kafka integration for input/output streams
- PacketInspector adapter for network sensor data
- Comprehensive logging and health checks
- Graceful shutdown and error handling
- Unit tests covering pipeline orchestration

## Architecture
- **.NET 8 Worker Service** with Dependency Injection
- **Pipeline Pattern**: Pluggable processors for different data sources
- **Kafka Integration**: Uses Sakin.Messaging library for reliable streaming
- **Configuration**: appsettings.json with environment-specific overrides
- **Health Checks**: Built-in health monitoring
- **Logging**: Structured logging with configurable levels

## Components

### Pipeline Architecture
- `IEventPipeline`: Main pipeline orchestrator
- `IEventSource`: Kafka-based event source (subscribes to `sakin.raw.events`)
- `IEventSink`: Kafka-based event sink (publishes to `sakin.normalized.events`)
- `IEventProcessor`: Pluggable processors for different data formats

### Processors
- `PacketInspectorProcessor`: Processes network sensor data into `NetworkEvent` objects

### Services
- `IngestService`: Main background service orchestrating the pipeline
- Health checks and graceful shutdown

## Configuration

### Key Settings
- `Kafka.BootstrapServers`: Kafka broker connection
- `Kafka.Consumer.GroupId`: Consumer group ID
- `Ingestion.InputTopic`: Raw events topic (`sakin.raw.events`)
- `Ingestion.OutputTopic`: Normalized events topic (`sakin.normalized.events`)
- `Ingestion.BatchSize`: Processing batch size
- `Ingestion.FlushIntervalSeconds`: Flush interval for batches

### Environment Variables
- `Kafka__BootstrapServers`: Kafka connection string
- `Ingestion__BatchSize`: Override batch size
- `Logging__LogLevel__Default`: Set logging level

## Integration
- **Input**: Subscribes to `sakin.raw.events` topic from network sensors and collectors
- **Output**: Publishes normalized events to `sakin.normalized.events` topic
- **Dependencies**: PostgreSQL (for configuration), Kafka (for messaging)

## Development

### Building and Running
```bash
# Build the service
dotnet build

# Run locally (requires Kafka)
dotnet run

# Run tests
dotnet test
```

### Testing
- Unit tests for pipeline orchestration
- Processor-specific tests
- Mock-based testing for external dependencies
- Test coverage for error scenarios

## Deployment
- Docker container with multi-stage build
- Configured for Docker Compose environment
- Health checks and graceful shutdown
- Environment-specific configuration
