# Sakin Correlation

## Overview
Event correlation and threat detection engine for identifying security patterns and anomalies in real-time.

## Purpose
This service performs:
- Real-time event correlation across multiple data sources
- Security rule engine for threat detection
- Alert generation and severity scoring
- Pattern matching based on configurable rules
- Stateful event tracking using Redis

## Status
✅ **Implemented** - Core correlation engine with async processing pipeline.

## Implemented Features
- Async processing pipeline with backpressure controls
- Configurable correlation rules (JSON)
- Time-window based aggregation with Redis state management
- Alert generation and publishing to Kafka
- Batching and concurrency configuration
- Multiple rule evaluation with conditions
- Group-by field support for correlation
- Unit tests for pipeline and rule engine

## Architecture
- **Worker Service**: .NET 8.0 background service consuming from Kafka
- **Pipeline**: Channel-based async processing with configurable parallelism
- **Rule Engine**: Evaluates events against correlation rules
- **State Manager**: Redis-backed stateful tracking for time-windowed aggregation
- **Alert Publisher**: Publishes alerts to Kafka topics
- **Alert Repository**: Log-based persistence (placeholder for Postgres)

## Configuration

### Kafka Options
- `BootstrapServers`: Kafka broker addresses
- `ConsumerGroup`: Consumer group ID for normalized events
- `NormalizedEventsTopic`: Input topic for normalized events
- `AlertsTopic`: Output topic for generated alerts

### Pipeline Options
- `MaxDegreeOfParallelism`: Number of concurrent processing workers (default: 4)
- `ChannelCapacity`: Internal buffer size for backpressure (default: 256)
- `BatchSize`: Number of events to process in a batch (default: 50)
- `BatchIntervalMilliseconds`: Max time to wait before flushing a partial batch (default: 1000ms)

### Rules Options
- `RulesDirectory`: Directory containing rule JSON files (default: ./rules)
- `TimeWindowSeconds`: Default time window for correlation (default: 300s)
- `MinEventsForCorrelation`: Minimum events to trigger correlation (default: 2)
- `StateExpirationSeconds`: How long to retain state in Redis (default: 600s)

### Redis Options
- `ConnectionString`: Redis connection string (default: localhost:6379)

## Rule Format
Rules are defined in JSON format:
```json
{
  "id": "rule-id",
  "name": "Rule Name",
  "description": "Rule description",
  "severity": "High|Medium|Low",
  "enabled": true,
  "minEventCount": 5,
  "timeWindowSeconds": 300,
  "conditions": [
    {
      "field": "EventType",
      "operator": "Equals",
      "value": "AuthenticationAttempt"
    }
  ],
  "groupByFields": ["SourceIp"],
  "tags": ["tag1", "tag2"]
}
```

## Integration
- **Input**: Normalized events from `sakin-ingest` via Kafka `normalized-events` topic
- **Output**: Security alerts to Kafka `alerts` topic and log output
- **State Storage**: Correlation state maintained in Redis with TTL
- **Future**: Alert persistence to PostgreSQL

## Development
```bash
cd sakin-correlation/Sakin.Correlation
dotnet run
```

## Testing
```bash
cd sakin-correlation/Sakin.Correlation.Tests
dotnet test
```
