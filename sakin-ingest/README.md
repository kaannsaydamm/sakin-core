# Sakin Ingest Worker Service

## Overview
The Sakin Ingest worker is the entry point to the normalization pipeline. It consumes raw event envelopes from Kafka, applies the (currently trivial) normalization pass-through logic, and publishes the result to the downstream normalized events topic. This skeleton focuses on wiring together the messaging layer and establishing the background service infrastructure.

## Current Capabilities
- ✅ Kafka consumer subscribed to the `raw-events` topic
- ✅ Logs every received envelope for observability
- ✅ Copies the raw payload into a `NormalizedEvent` placeholder
- ✅ Publishes the updated envelope to the `normalized-events` topic using the shared Kafka producer
- ✅ Reuses shared models from `Sakin.Common` and messaging abstractions from `Sakin.Messaging`

## Project Layout
```
/sakin-ingest
  ├── Sakin.Ingest/                # .NET 8 worker service project
  │   ├── Program.cs               # Host builder & dependency injection
  │   ├── Workers/EventIngestWorker.cs
  │   ├── Configuration/IngestKafkaOptions.cs
  │   ├── appsettings.json
  │   └── appsettings.Development.json
  ├── appsettings.json             # Default configuration (mirrors project file)
  ├── appsettings.Development.json # Development overrides
  └── Dockerfile                   # Multi-stage build for the worker
```

## Configuration
The worker uses `appsettings.json` (with optional `appsettings.Development.json`) and environment variables. Core Kafka settings live under the `Kafka` section:

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "RawEventsTopic": "raw-events",
    "NormalizedEventsTopic": "normalized-events",
    "ConsumerGroup": "ingest-service",
    "ClientId": "sakin-ingest"
  }
}
```

Environment variables can override any value using the standard double underscore notation, e.g. `Kafka__BootstrapServers` or `Kafka__ConsumerGroup`.

## Running Locally
1. Ensure Kafka is available (e.g. `docker compose -f deployments/docker-compose.dev.yml up kafka`).
2. Restore and run the worker:
   ```bash
   dotnet restore
   dotnet run --project sakin-ingest/Sakin.Ingest/Sakin.Ingest.csproj
   ```
3. The worker will begin consuming from `raw-events` and publishing to `normalized-events`.

### Using Docker
Build and run the containerised worker:
```bash
docker build -t sakin-ingest ./sakin-ingest
docker run --rm \
  -e Kafka__BootstrapServers=host.docker.internal:9092 \
  -e Kafka__RawEventsTopic=raw-events \
  -e Kafka__NormalizedEventsTopic=normalized-events \
  -e Kafka__ConsumerGroup=ingest-service \
  sakin-ingest
```

## Next Steps
- Implement real normalization and enrichment logic
- Add validation using the event schema from `Sakin.Common`
- Integrate metrics and health checks
- Expand configuration to cover retry policies, batching, and advanced consumer settings

This skeleton provides the foundations for those enhancements while ensuring the ingest service is already wired into the messaging ecosystem.
