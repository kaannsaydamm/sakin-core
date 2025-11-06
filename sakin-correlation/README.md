# Sakin Correlation Engine

## Overview

The correlation engine is the heart of S.A.K.I.N.'s threat detection capabilities. It consumes normalized events from Kafka and applies sophisticated rule-based correlation to identify security threats, anomalies, and suspicious patterns.

## Key Features

### Real-Time Rule Engine
- **Stateless Rules**: Single-event pattern matching with immediate alert generation
- **Stateful Rules**: Redis-backed time-windowed aggregation (brute-force detection, data exfiltration)
- **Rule DSL**: Declarative JSON-based rule language with intuitive operators
- **Dynamic Threshold**: Configurable aggregation windows (1-3600 seconds)

### Threat Detection Capabilities
- 🔴 **Brute-force Detection**: Failed login aggregation with configurable threshold
- 🔴 **Data Exfiltration**: Large data transfer detection across hosts
- 🔴 **Privilege Escalation**: Multiple failure pattern detection
- 🔴 **Lateral Movement**: Connection pattern anomalies
- 🔴 **C2 Communication**: DNS/HTTP anomaly detection

### Risk Scoring (Sprint 6+)
- Asset criticality-based scoring
- Threat intelligence reputation lookups
- Time-of-day behavioral analysis
- User risk profiling
- Anomaly scoring boost (Sprint 7)

### Anomaly Detection (Sprint 7)
- ML-based baseline detection with Z-score calculation
- Statistical analysis of user behavior patterns
- Connection count and port usage anomalies
- 0-100 normalized anomaly scores with reasoning

### Alert Management (Sprint 7)
- Alert deduplication with configurable time windows
- Status lifecycle machine (New → Acknowledged → Under Investigation → Resolved)
- Full audit trail with user and timestamp tracking
- Rich alert details with enrichment data

## Architecture

```
[Normalized Events] ──▶ Kafka (normalized-events topic)
         │
         ▼
[Correlation Worker]
    ├─▶ [Stateless Rule Engine] ──▶ Immediate Alerts
    ├─▶ [Stateful Rules + Redis] ──▶ Time-windowed Alerts
    └─▶ [Risk Scoring Service]
         ├─ Asset Enrichment
         ├─ Threat Intel
         ├─ Time-of-Day Analysis
         ├─ Anomaly Detection
         └─ Risk Score (0-100)
         │
         ▼
    [Alert Deduplication]
         │
         ▼
    [Alert Persistence]
         │
         ▼
    [PostgreSQL + Redis]
         │
         ▼
    [Alert Notification] ──▶ Kafka (alerts topic)
                    │
                    ▼
            [SOAR + Panel API]
```

## Rule Development

### Rule Structure

```json
{
  "Id": "rule-brute-force-detection",
  "Name": "Brute Force Detection",
  "Description": "Detects multiple failed login attempts",
  "Enabled": true,
  "RuleType": "Stateful",
  "WindowSeconds": 300,
  "Conditions": [
    {
      "Field": "EventType",
      "Operator": "Equals",
      "Value": "AuthenticationFailure"
    }
  ],
  "GroupBy": ["SourceIp", "Username"],
  "Threshold": 5,
  "Severity": "High",
  "AlertMessage": "Brute force attack detected: {threshold} failed logins in {window}s from {SourceIp}"
}
```

### Operators

- **Equals, NotEquals**: String/numeric comparison
- **Contains, NotContains**: String matching
- **GreaterThan, LessThan**: Numeric comparison
- **StartsWith, EndsWith**: String prefix/suffix
- **In, NotIn**: List membership
- **Regex**: Regular expression matching
- **Exists**: Field presence check

### Examples

**Stateless Rule (Single Event)**
```json
{
  "RuleType": "Stateless",
  "Conditions": [
    {"Field": "EventType", "Operator": "Equals", "Value": "PrivilegeEscalation"},
    {"Field": "Severity", "Operator": "GreaterThan", "Value": 7}
  ]
}
```

**Stateful Rule (Time Aggregation)**
```json
{
  "RuleType": "Stateful",
  "WindowSeconds": 600,
  "GroupBy": ["SourceIp"],
  "Threshold": 10,
  "Conditions": [
    {"Field": "EventType", "Operator": "Equals", "Value": "PortScan"}
  ]
}
```

See [Rule Development Guide](../docs/rule-development.md) for comprehensive documentation.

## Configuration

### appsettings.json

```json
{
  "Kafka": {
    "BootstrapServers": "kafka:9092",
    "NormalizedEventsTopic": "normalized-events",
    "AlertsTopic": "alerts",
    "ConsumerGroup": "correlation-service"
  },
  "Redis": {
    "ConnectionString": "redis:6379",
    "InstanceName": "sakin:"
  },
  "Database": {
    "ConnectionString": "Server=postgres;Database=sakin;User Id=sakin;Password=password;"
  },
  "RuleEngine": {
    "RulesPath": "./rules",
    "MaxConcurrentEvaluations": 100,
    "StatefulWindowCleanupIntervalSeconds": 3600
  },
  "AnomalyDetection": {
    "Enabled": true,
    "ZScoreThreshold": 2.5,
    "CacheDurationSeconds": 60,
    "AnomalyMaxBoost": 20.0,
    "RedisKeyPrefix": "sakin:anomaly"
  },
  "OpenTelemetry": {
    "Enabled": true,
    "OtlpEndpoint": "http://localhost:4318"
  }
}
```

### Environment Variables

- `Kafka__BootstrapServers`: Kafka broker connection
- `Kafka__NormalizedEventsTopic`: Input topic for normalized events
- `Kafka__AlertsTopic`: Output topic for generated alerts
- `Redis__ConnectionString`: Redis connection
- `Database__ConnectionString`: PostgreSQL connection
- `ASPNETCORE_ENVIRONMENT`: Runtime environment (Development/Production)
- `OTEL_EXPORTER_OTLP_ENDPOINT`: OpenTelemetry collector endpoint

## Running Locally

### Prerequisites
- Docker (for Kafka, Redis, PostgreSQL)
- .NET 8 SDK

### Steps

```bash
# 1. Start infrastructure
cd deployments
docker compose -f docker-compose.dev.yml up -d

# 2. Verify services
./scripts/verify-services.sh

# 3. Initialize database
psql -f scripts/postgres/01-init-database.sql

# 4. Load rules
cp rules/*.json rules/

# 5. Run correlation service
cd ../sakin-correlation/Sakin.Correlation
dotnet run
```

## Performance Characteristics

- **Rule Evaluation**: 10,000+ rules/second
- **Latency (p99)**: <50ms from normalized event to alert
- **Memory Usage**: ~500MB baseline + Redis state
- **Throughput**: Handles 1000+ EPS with sub-100ms ingestion latency
- **Scalability**: Horizontally scalable via Kafka consumer groups

## Development

### Building

```bash
dotnet build sakin-correlation/Sakin.Correlation/Sakin.Correlation.csproj
```

### Testing

```bash
dotnet test tests/Sakin.Correlation.Tests/Sakin.Correlation.Tests.csproj
```

### Adding Custom Rules

1. Create JSON rule file in `sakin-correlation/rules/`
2. Define conditions and thresholds
3. Restart service or reload via API
4. Monitor alerts via Panel UI

## Integration

### Input
- **Kafka Topic**: `normalized-events`
- **Format**: NormalizedEvent with enrichment data (GeoIP, Threat Intel)

### Output
- **Kafka Topic**: `alerts`
- **Format**: AlertEntity with risk score, lifecycle status, audit trail

### Dependencies
- **PostgreSQL**: Alert storage and lifecycle tracking
- **Redis**: Stateful rule state, baseline caching, session aggregation
- **Kafka**: Event streaming and alert publishing
- **GeoIP Database**: Location enrichment (optional)
- **Threat Intel APIs**: IP reputation (optional)

## Monitoring

### Metrics
- `correlation_rules_evaluated_total`: Total rules evaluated
- `correlation_alerts_generated_total`: Alerts generated by rule
- `correlation_processing_duration_seconds`: Processing time per event
- `correlation_redis_operations_total`: Redis state operations

### Health Checks
- `GET /healthz`: Service health status
- Kafka connectivity
- Redis connectivity
- Database connectivity
- Rule loading status

### Logs
Structured JSON logs via Serilog:
- Rule evaluation traces
- Alert generation events
- Error handling and exceptions
- Performance metrics

## Troubleshooting

### High Latency
1. Check Kafka lag: `./scripts/check-kafka-lag.sh`
2. Monitor Redis memory: `redis-cli INFO memory`
3. Profile CPU usage during peaks
4. Consider scaling to multiple instances

### Missing Alerts
1. Verify rules are loaded: `GET /api/rules`
2. Check rule conditions match events
3. Review rule evaluation logs
4. Verify Kafka topic connectivity

### Redis State Issues
1. Clear stale state: `redis-cli FLUSHDB` (careful in production!)
2. Check memory usage: `redis-cli INFO memory`
3. Review cleanup intervals
4. Monitor command latency

## Further Reading

- [Comprehensive Architecture Guide](../docs/architecture.md)
- [Rule Development Guide](../docs/rule-development.md)
- [Anomaly Detection Details](../docs/anomaly-detection.md)
- [Alert Lifecycle Management](../docs/alert-lifecycle.md)
- [Monitoring Guide](../docs/monitoring.md)
