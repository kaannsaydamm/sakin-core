# Network Sensor Service

A .NET 8 worker service that monitors network interfaces, captures packet metadata, and streams events to Kafka in the SAKIN event envelope format. PostgreSQL persistence can be enabled for legacy workflows or fallback scenarios.

## Overview

The network sensor leverages SharpPcap and PacketDotNet to inspect packets in promiscuous mode. Each captured packet is transformed into a normalized `EventEnvelope` and published to the `raw-events` Kafka topic via the shared `Sakin.Messaging` library. When PostgreSQL writes are enabled, the sensor also mirrors summary data and extracted SNI values to the database.

## Architecture

- **Dependency Injection**: Uses `Microsoft.Extensions.DependencyInjection` across handlers, messaging, and services.
- **Kafka Integration**: `EventPublisher` batches packet events and publishes through `IKafkaProducer` with retry and fallback logging.
- **Optional PostgreSQL Persistence**: Controlled through configuration, allowing deployments to run Kafka-only or Kafka + Postgres.
- **Configuration Driven**: Strongly typed options for database, Kafka, batching, and retries.
- **Graceful Lifecycle**: Hosted service pattern with coordinated shutdown and producer flush.

## Configuration

`appsettings.json` (and environment-specific overrides) provide all runtime configuration:

```json
{
  "Database": {
    "Host": "localhost",
    "Username": "postgres",
    "Password": "your_password",
    "Database": "network_db",
    "Port": 5432
  },
  "Postgres": {
    "WriteEnabled": true
  },
  "Kafka": {
    "Enabled": true,
    "BootstrapServers": "localhost:9092",
    "ClientId": "network-sensor",
    "RawEventsTopic": "raw-events",
    "BatchSize": 100,
    "FlushIntervalMs": 1000,
    "RetryCount": 3,
    "RetryBackoffMs": 200
  },
  "KafkaProducer": {
    "DefaultTopic": "raw-events"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

### Environment Overrides

Use environment variables to override settings when running in containers or CI:

- `Database__Host`, `Database__Username`, `Database__Password`, `Database__Database`, `Database__Port`
- `Postgres__WriteEnabled`
- `Kafka__Enabled`, `Kafka__BootstrapServers`, `Kafka__RawEventsTopic`, `Kafka__BatchSize`, `Kafka__FlushIntervalMs`
- `KafkaProducer__DefaultTopic`

Setting `Postgres__WriteEnabled=false` disables all database writes while keeping Kafka publishing active.

## Event Format

Each packet is wrapped in `Sakin.Common.Models.EventEnvelope`:

- **Raw**: Base64 encoded payload plus packet metadata (source/destination, ports, protocol, timestamps, captured SNI).
- **Normalized**: Populated `NormalizedEvent` containing IP addresses, ports, protocol, device/sensor identifiers, and lightweight metadata.
- **Retries & Fallbacks**: Kafka publishes retry up to three times. After repeated failure the packet contents are logged locally for diagnostics.

This format is compatible with the ingest worker (`sakin-ingest`) which listens on the `raw-events` topic.

## Running the Sensor

```bash
docker-compose up -d  # brings up Kafka, Postgres, and supporting services

# In another terminal
dotnet run --project sakin-core/services/network-sensor/Sakin.Core.Sensor.csproj
```

To validate the Kafka pipeline, start a consumer against the `raw-events` topic:

```bash
kafka-console-consumer --bootstrap-server localhost:9092 --topic raw-events --from-beginning
```

You should observe JSON envelopes streaming in as network traffic is generated.

## Components

- **NetworkSensorService**: Primary hosted service that controls capture lifecycle and graceful shutdown.
- **PackageInspector**: Transforms packets into `PacketEventData`, extracts SNI, and coordinates Kafka/Postgres flows.
- **EventPublisher**: Batches packet events, performs retry logic, and publishes envelopes to Kafka.
- **DatabaseHandler**: Optional PostgreSQL persistence for packet and SNI records.
- **Configuration**: `SensorKafkaOptions` and `PostgresOptions` enable fine-grained feature toggles.

## Notes & Migration History

This service was modernised from the legacy SAKINCore-CS implementation with the following enhancements:

- Kafka-first ingestion pipeline using the shared messaging library
- Strongly typed configuration with feature flags for Postgres writes
- Structured logging throughout packet processing and publishing
- Graceful shutdown that flushes the Kafka producer
- Event envelope alignment with downstream ingest and analytics services

Ensure the service runs with sufficient privileges to access network interfaces (e.g. `sudo` on Linux/macOS).
