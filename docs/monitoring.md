# Monitoring & Observability Guide

## Overview

S.A.K.I.N. provides comprehensive monitoring through OpenTelemetry, Prometheus metrics, Grafana dashboards, and Jaeger tracing.

## Quick Access

- **Grafana Dashboards**: http://localhost:3000 (admin/admin)
- **Prometheus**: http://localhost:9090
- **Jaeger Traces**: http://localhost:16686

## OpenTelemetry Integration

### Configuration

All services export metrics and traces to OpenTelemetry collector:

```json
{
  "OpenTelemetry": {
    "Enabled": true,
    "OtlpEndpoint": "http://otel-collector:4318",
    "ServiceName": "sakin-correlation",
    "Sampler": 0.1
  },
  "Logging": {
    "Console": true,
    "Otlp": true,
    "JsonFormat": true
  }
}
```

### Environment Variables

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318
OTEL_SERVICE_NAME=sakin-correlation
OTEL_TRACES_SAMPLER=parentbased_traceidratio
OTEL_TRACES_SAMPLER_ARG=0.1
```

## Prometheus Metrics

### Available Metrics

#### Ingestion Service

```
sakin_ingest_events_processed_total{parser="windows", status="success"}
sakin_ingest_enrichment_duration_seconds{service="geoip"}
sakin_ingest_threat_intel_lookups_total{provider="otx", status="hit"}
sakin_ingest_parsing_errors_total{parser="syslog"}
```

#### Correlation Service

```
sakin_correlation_rules_evaluated_total{rule_id="rule-id", outcome="match"}
sakin_correlation_alerts_generated_total{rule_id="rule-id", severity="High"}
sakin_correlation_rule_evaluation_duration_seconds{rule_id="rule-id"}
sakin_correlation_redis_operations_total{operation="set", status="success"}
sakin_correlation_anomaly_scores_total{is_anomalous="true"}
```

#### SOAR Service

```
sakin_soar_playbook_executions_total{playbook_id="playbook-id", status="success"}
sakin_soar_playbook_step_duration_seconds{playbook_id="playbook-id", step_id="step-id"}
sakin_soar_notifications_sent_total{channel="slack", status="success"}
sakin_soar_agent_commands_executed_total{command="block_ip", status="success"}
```

#### Panel API

```
sakin_panel_request_duration_seconds{method="GET", endpoint="/api/alerts"}
sakin_panel_alerts_queried_total{status_filter="New"}
sakin_panel_status_transitions_total{from_status="New", to_status="Acknowledged"}
```

#### Infrastructure

```
process_resident_memory_bytes
process_cpu_seconds_total
dotnet_gc_collection_count_total
dotnet_threadpool_scheduled_actions_total
```

### Query Examples

#### Event Throughput

```promql
# Events per second
rate(sakin_ingest_events_processed_total[1m])

# Top parsers by volume
topk(5, rate(sakin_ingest_events_processed_total[5m]) by (parser))
```

#### Latency

```promql
# p95 rule evaluation latency
histogram_quantile(0.95, rate(sakin_correlation_rule_evaluation_duration_seconds_bucket[5m]))

# Average correlation latency
avg(sakin_correlation_rule_evaluation_duration_seconds_sum / sakin_correlation_rule_evaluation_duration_seconds_count)
```

#### Errors

```promql
# Error rate
rate(sakin_ingest_parsing_errors_total[5m])

# Failed playbook executions
rate(sakin_soar_playbook_executions_total{status="failure"}[5m])
```

#### Resource Usage

```promql
# Memory usage per service
container_memory_usage_bytes{pod=~"sakin-.*"}

# CPU usage
rate(process_cpu_seconds_total[5m])
```

## Grafana Dashboards

### 1. Alerts Overview

**Metrics:**
- Alert rate (per minute)
- Top rules by alert volume
- Severity distribution (pie chart)
- Alert trend (24h)

**Panels:**
```
- Alerts/sec: rate(sakin_correlation_alerts_generated_total[1m])
- By Severity: sum by (severity) (sakin_correlation_alerts_generated_total)
- Top Rules: topk(10, sakin_correlation_alerts_generated_total)
```

### 2. Playbook Execution

**Metrics:**
- Playbook execution rate
- Success/failure rates
- Step execution times
- Notification delivery status

**Panels:**
```
- Playbook Success Rate: 100 * rate(sakin_soar_playbook_executions_total{status="success"}[1m]) / rate(sakin_soar_playbook_executions_total[1m])
- Step Latency (p95): histogram_quantile(0.95, rate(sakin_soar_playbook_step_duration_seconds_bucket[5m]))
- Notifications Sent: rate(sakin_soar_notifications_sent_total[1m])
```

### 3. Anomaly Detection

**Metrics:**
- Baseline vs actual values
- Z-scores over time
- Anomaly detection rate

**Panels:**
```
- Anomalous Events: rate(sakin_correlation_anomaly_scores_total{is_anomalous="true"}[5m])
- Average Z-Score: avg(sakin_correlation_anomaly_z_score)
```

### 4. System Health

**Metrics:**
- Memory usage per service
- CPU usage
- Database connection pool
- Kafka lag

**Panels:**
```
- Memory: container_memory_usage_bytes
- CPU: rate(process_cpu_seconds_total[5m])
- Kafka Lag: kafka_consumergroup_lag_sum
```

## Health Checks

### Endpoint

All services expose health check at `/healthz`:

```bash
curl http://localhost:5000/healthz
```

**Response:**
```json
{
  "status": "healthy",
  "checks": {
    "database": {
      "status": "healthy",
      "responseTime": "5ms"
    },
    "kafka": {
      "status": "healthy",
      "responseTime": "10ms"
    },
    "redis": {
      "status": "healthy",
      "responseTime": "2ms"
    }
  },
  "timestamp": "2024-11-06T10:30:45Z"
}
```

### Kubernetes Probes

```yaml
livenessProbe:
  httpGet:
    path: /healthz
    port: 5000
  initialDelaySeconds: 30
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /healthz
    port: 5000
  initialDelaySeconds: 5
  periodSeconds: 5
```

## Alert Rules

### Configured Alerts

#### Service Health

```yaml
groups:
  - name: sakin.rules
    rules:
      - alert: ServiceDown
        expr: up{job="sakin"} == 0
        for: 1m
        annotations:
          summary: "S.A.K.I.N. service {{ $labels.instance }} is down"
```

#### Latency

```yaml
- alert: HighCorrelationLatency
  expr: |
    histogram_quantile(0.99, rate(sakin_correlation_rule_evaluation_duration_seconds_bucket[5m]))
    > 0.5
  annotations:
    summary: "Correlation latency p99 > 500ms"
```

#### Errors

```yaml
- alert: HighErrorRate
  expr: |
    rate(sakin_ingest_parsing_errors_total[5m])
    / rate(sakin_ingest_events_processed_total[5m])
    > 0.01
  annotations:
    summary: "Parsing error rate > 1%"
```

#### Resource

```yaml
- alert: HighMemoryUsage
  expr: |
    container_memory_usage_bytes / container_spec_memory_limit_bytes
    > 0.9
  annotations:
    summary: "Memory usage > 90%"
```

## Log Aggregation

### Serilog Configuration

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      },
      {
        "Name": "OpenTelemetry",
        "Args": {
          "endpoint": "http://otel-collector:4317"
        }
      }
    ],
    "Enrich": [
      "FromLogContext",
      "WithMachineName",
      "WithThreadId",
      "WithCorrelationId"
    ]
  }
}
```

### Log Fields

Every log includes:
- `timestamp`: ISO8601 timestamp
- `level`: Log level (Info, Warning, Error, etc)
- `service`: Service name
- `correlationId`: Trace correlation ID
- `userId`: User performing action
- `action`: Action being performed
- `duration`: Operation duration (ms)
- `result`: Success/failure status

**Example Log:**
```json
{
  "timestamp": "2024-11-06T10:30:45.123Z",
  "level": "Information",
  "service": "sakin-correlation",
  "correlationId": "b3d4e5f6-7890-1234",
  "action": "EvaluateRule",
  "ruleId": "rule-brute-force",
  "duration": 2.5,
  "result": "Matched",
  "alertId": "alert-uuid-123"
}
```

## Distributed Tracing

### Jaeger Setup

Traces are automatically sent from all services:

```
http://localhost:16686
```

### Trace Visualization

1. Select service: `sakin-correlation`
2. Select operation: `EvaluateRules`
3. Set time range and search

**Shows:**
- Full trace path through services
- Latency breakdown per service
- Error details
- Log events

### Custom Traces

Add custom spans in code:

```csharp
using System.Diagnostics;

var span = new ActivitySource("sakin").StartActivity("CustomOperation");
try
{
    // Do work
    span?.SetTag("result", "success");
}
finally
{
    span?.Dispose();
}
```

## Performance Monitoring

### Key Metrics to Track

| Metric | Target | Alert > |
|--------|--------|---------|
| Ingestion Latency (p99) | <100ms | 150ms |
| Correlation Latency (p99) | <50ms | 100ms |
| Rule Evaluation Rate | >10k/sec | N/A |
| Alert Query Latency (p99) | <500ms | 1000ms |
| Error Rate | <0.1% | 1% |
| CPU Usage | <50% | 80% |
| Memory Usage | <70% | 90% |
| Kafka Lag | <5s | 30s |

### Performance Profiling

```bash
# Enable verbose logging
ASPNETCORE_LOGLEVEL=Debug docker compose restart correlation

# Monitor resource usage
docker stats correlation

# Check Kafka lag
docker exec kafka kafka-consumer-groups \
  --bootstrap-server kafka:9092 \
  --group correlation-service \
  --describe
```

## Custom Metrics

### Adding Metrics to Service

```csharp
// Create meter
private static readonly Meter Meter = new("Sakin.CustomService");

// Counter
private readonly Counter<int> _alertCounter = Meter.CreateCounter<int>("alerts_generated");

// Histogram
private readonly Histogram<double> _latency = Meter.CreateHistogram<double>("operation_duration_ms");

// Usage
_alertCounter.Add(1, new KeyValuePair<string, object?>("severity", "High"));
_latency.Record(elapsed.TotalMilliseconds);
```

## Alerting Strategy

### Alerting Best Practices

1. **Alert on Symptoms, Not Causes**
   - Alert on high latency, not CPU spike
   - Alert on error rate, not individual errors

2. **Clear Actionable Alerts**
   - Include context (service, threshold, current value)
   - Include remediation steps

3. **Runbook Links**
   ```yaml
   annotations:
     runbook_url: "https://wiki.company.com/sakin/high-latency"
   ```

4. **Alert Routing**
   ```yaml
   alerting:
     alertmanagers:
       - static_configs:
           - targets: ['alertmanager:9093']
   ```

## Maintenance

### Regular Tasks

**Daily:**
- Review alert volume
- Check error rates
- Verify disk space

**Weekly:**
- Review performance trends
- Check trace sampling
- Verify backup completion

**Monthly:**
- Capacity planning review
- Alert tuning
- Documentation updates

### Log Cleanup

```bash
# Archive old logs (OpenSearch)
DELETE /sakin-logs-2024.10.* 

# Clean old traces (Jaeger)
curl -X POST http://localhost:14250/api/traces/delete \
  -H "Content-Type: application/json" \
  -d '{"older_than_days": 30}'
```

---

**See Also:**
- [Troubleshooting Guide](./troubleshooting.md)
- [Architecture Overview](./architecture.md)
- [Deployment Guide](./deployment.md)
