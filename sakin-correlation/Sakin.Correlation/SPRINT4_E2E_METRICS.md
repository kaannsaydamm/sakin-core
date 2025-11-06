# Sprint 4: Correlation E2E & Metrics

## Overview

This document describes the end-to-end correlation flow from Kafka ingestion to alert persistence, along with the metrics implementation for monitoring the correlation engine.

## Architecture

```
Kafka (normalized-events) 
    ↓
Worker.cs (ConsumerLoop)
    ↓
RuleLoaderService/V2 (loads rules from filesystem)
    ↓
RuleEvaluatorV2 (evaluates both stateless and aggregation rules)
    ↓
├─ Stateless Rules → Direct alert
└─ Aggregation Rules → RedisStateManager → Threshold check → Alert
    ↓
AlertCreatorService
    ↓
AlertRepository → PostgreSQL
```

## Components

### 1. Worker (Kafka Consumer)

The `Worker.cs` class is the main entry point that:
- Consumes events from the `normalized-events` Kafka topic
- Evaluates each event against loaded rules (both legacy and V2 format)
- Creates alerts when rules match
- Tracks metrics for events processed, rules evaluated, and processing latency

### 2. Rule Evaluation

**Stateless Rules (Legacy Format):**
- Simple field matching (e.g., event_type equals "authentication_failure")
- Immediate alert creation on match
- Example: `simple-failed-login.json`

**Aggregation Rules (V2 Format):**
- Count events matching criteria over a time window
- Group by specific fields (e.g., source IP)
- Trigger alerts when threshold is reached
- Example: `rdp-bruteforce-v2.json` (>= 10 failed logins per IP in 300s)

### 3. Redis State Management

For aggregation rules, the `RedisStateManager` maintains counters using the key pattern:
```
{keyPrefix}rule:{ruleId}:group:{groupValue}:window:{windowId}
```

Example:
```
sakin:correlation:rule:rule-bruteforce-01:group:203.0.113.42:window:1705314000
```

### 4. Alert Persistence

The `AlertCreatorService` persists alerts to PostgreSQL via the `AlertRepository`:
- Stores alert metadata (rule ID, severity, triggered time)
- Captures event context
- Tracks aggregation count and values (for aggregation rules)

## Metrics

The correlation engine exposes Prometheus-compatible metrics at `/metrics`:

### Available Metrics

| Metric Name | Type | Description |
|-------------|------|-------------|
| `sakin_correlation_events_processed_total` | Counter | Total number of events consumed from Kafka |
| `sakin_correlation_rules_evaluated_total` | Counter | Total number of rule evaluations performed |
| `sakin_correlation_alerts_created_total` | Counter | Total number of alerts created and persisted |
| `sakin_correlation_redis_ops_total` | Counter | Total number of Redis operations (INCR, GET, SET) |
| `sakin_correlation_processing_latency_ms` | Histogram | Event processing latency in milliseconds |

### Accessing Metrics

```bash
# View metrics
curl http://localhost:8080/metrics

# Sample output
# HELP sakin_correlation_events_processed_total Total number of events processed
# TYPE sakin_correlation_events_processed_total counter
sakin_correlation_events_processed_total 42

# HELP sakin_correlation_rules_evaluated_total Total number of rules evaluated
# TYPE sakin_correlation_rules_evaluated_total counter
sakin_correlation_rules_evaluated_total 126

# HELP sakin_correlation_alerts_created_total Total number of alerts created
# TYPE sakin_correlation_alerts_created_total counter
sakin_correlation_alerts_created_total 5

# HELP sakin_correlation_processing_latency_ms Processing latency in milliseconds
# TYPE sakin_correlation_processing_latency_ms histogram
sakin_correlation_processing_latency_ms_bucket{le="1"} 10
sakin_correlation_processing_latency_ms_bucket{le="2"} 25
sakin_correlation_processing_latency_ms_bucket{le="4"} 38
sakin_correlation_processing_latency_ms_bucket{le="8"} 40
sakin_correlation_processing_latency_ms_bucket{le="+Inf"} 42
sakin_correlation_processing_latency_ms_sum 120.5
sakin_correlation_processing_latency_ms_count 42
```

## Health Checks

The correlation engine exposes a health endpoint at `/health`:

```bash
curl http://localhost:8080/health
# Returns: Healthy
```

## Local Development Setup

### Prerequisites
- Docker and Docker Compose
- .NET 8.0 SDK (for local builds)

### Quick Start

1. **Start all services (Kafka, Redis, PostgreSQL, Correlation Engine):**
   ```bash
   ./scripts/run-correlation-dev.sh up
   ```

2. **Publish sample events to Kafka:**
   ```bash
   ./scripts/publish-sample-events.sh
   ```

3. **View correlation engine logs:**
   ```bash
   ./scripts/run-correlation-dev.sh logs
   ```

4. **Check metrics:**
   ```bash
   curl http://localhost:8080/metrics
   ```

5. **Query alerts from database:**
   ```bash
   docker exec -it sakin-postgres-correlation psql -U postgres -d sakin_correlation \
     -c 'SELECT id, rule_id, severity, triggered_at FROM public.alerts ORDER BY triggered_at DESC LIMIT 10;'
   ```

### Available Commands

```bash
./scripts/run-correlation-dev.sh up       # Start services
./scripts/run-correlation-dev.sh down     # Stop services
./scripts/run-correlation-dev.sh logs     # View logs
./scripts/run-correlation-dev.sh restart  # Restart services
./scripts/run-correlation-dev.sh rebuild  # Rebuild correlation engine
./scripts/run-correlation-dev.sh clean    # Remove all containers and volumes
```

## Integration Tests

End-to-end integration tests are located in `tests/Sakin.Correlation.Tests/Integration/CorrelationE2ETests.cs`.

These tests use Testcontainers to spin up isolated instances of:
- PostgreSQL (for alert storage)
- Redis (for aggregation state)
- Kafka (for event streaming)

### Running Tests

```bash
cd /home/engine/project/tests/Sakin.Correlation.Tests
dotnet test --filter "FullyQualifiedName~CorrelationE2ETests"
```

### Test Cases

1. **StatelessRule_TriggersAlert_WhenEventMatches**
   - Publishes a single event matching a stateless rule
   - Verifies alert is created and persisted

2. **AggregationRule_TriggersAlert_WhenThresholdReached**
   - Publishes 6 events matching an aggregation rule (threshold = 5)
   - Verifies alert is created only when threshold is reached
   - Confirms Redis state tracking

3. **MetricsService_TracksProcessingMetrics**
   - Verifies metrics service increments counters correctly

## Sample Rules

### Stateless Rule Example

File: `configs/rules/simple-failed-login.json`

```json
{
  "id": "simple-failed-login",
  "name": "Failed Login Detection",
  "enabled": true,
  "severity": "medium",
  "triggers": [{
    "type": "event",
    "eventType": "authentication_failure"
  }],
  "conditions": [{
    "field": "normalized.action",
    "operator": "equals",
    "value": "login_failed"
  }],
  "actions": [{"type": "alert"}]
}
```

### Aggregation Rule Example (V2)

File: `configs/rules/rdp-bruteforce-v2.json`

```json
{
  "id": "rule-bruteforce-01",
  "name": "RDP Brute Force",
  "enabled": true,
  "trigger": {
    "source_types": ["windows-eventlog"],
    "match": {"event_code": "4625"}
  },
  "condition": {
    "aggregation": {
      "function": "count",
      "field": "Normalized.username",
      "group_by": "Normalized.source_ip",
      "window_seconds": 300
    },
    "operator": "gte",
    "value": 10
  },
  "severity": "high",
  "actions": [{"type": "alert"}]
}
```

## Configuration

Key configuration sections in `appsettings.json`:

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Topic": "normalized-events",
    "ConsumerGroup": "correlation-engine"
  },
  "Rules": {
    "RulesPath": "/path/to/configs/rules"
  },
  "Database": {
    "Host": "localhost",
    "Username": "postgres",
    "Password": "postgres",
    "Database": "sakin_correlation",
    "Port": 5432
  },
  "Redis": {
    "ConnectionString": "localhost:6379",
    "KeyPrefix": "sakin:correlation:",
    "DefaultTTL": 3600
  },
  "Aggregation": {
    "MaxWindowSize": 86400,
    "CleanupInterval": 300
  }
}
```

## Monitoring

### Prometheus Integration

Add the following to your `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'sakin-correlation'
    static_configs:
      - targets: ['localhost:8080']
    metrics_path: '/metrics'
    scrape_interval: 15s
```

### Grafana Dashboard Queries

Example queries for Grafana:

**Events Processing Rate:**
```promql
rate(sakin_correlation_events_processed_total[5m])
```

**Alert Creation Rate:**
```promql
rate(sakin_correlation_alerts_created_total[5m])
```

**Processing Latency (95th percentile):**
```promql
histogram_quantile(0.95, rate(sakin_correlation_processing_latency_ms_bucket[5m]))
```

**Redis Operations Rate:**
```promql
rate(sakin_correlation_redis_ops_total[5m])
```

## Troubleshooting

### No alerts being created

1. Check if events are being consumed:
   ```bash
   docker logs sakin-correlation-engine | grep "Consumed event"
   ```

2. Verify rules are loaded:
   ```bash
   docker logs sakin-correlation-engine | grep "Loaded.*rules"
   ```

3. Check if rules are matching:
   ```bash
   docker logs sakin-correlation-engine | grep "matched for event"
   ```

### Redis connection issues

```bash
# Test Redis connectivity
docker exec -it sakin-redis-correlation redis-cli ping

# Check Redis keys
docker exec -it sakin-redis-correlation redis-cli --scan --pattern "sakin:correlation:*"
```

### Database connection issues

```bash
# Test PostgreSQL connectivity
docker exec -it sakin-postgres-correlation pg_isready -U postgres -d sakin_correlation

# Check if migrations ran
docker exec -it sakin-postgres-correlation psql -U postgres -d sakin_correlation -c '\dt'
```

## Performance Considerations

1. **Kafka Consumer Configuration:**
   - Enable auto-commit for production
   - Adjust consumer group settings based on throughput

2. **Redis State:**
   - Keys automatically expire based on TTL
   - Background cleanup service runs periodically
   - Monitor Redis memory usage

3. **Database Writes:**
   - Alerts are written synchronously
   - Consider batching for high-volume scenarios
   - Monitor database connection pool

## Next Steps

1. Add Grafana dashboard templates
2. Implement alert deduplication
3. Add support for more aggregation functions (sum, avg, distinct)
4. Implement alert correlation between multiple rules
5. Add webhook/notification actions beyond database persistence
