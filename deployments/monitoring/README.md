# SAKIN Monitoring Stack

This directory contains configurations for the S.A.K.I.N platform's monitoring stack, including Prometheus, Grafana, Alertmanager, and Jaeger.

## Overview

The monitoring stack provides comprehensive observability for all SAKIN services:
- **Prometheus**: Metrics collection and alerting
- **Grafana**: Visualization and dashboards
- **Alertmanager**: Alert routing and notification management
- **Jaeger**: Distributed tracing (optional)

## Components

### Prometheus (`prometheus.yml`)

Prometheus configuration for scraping metrics from all SAKIN services on the `/metrics` endpoint.

**Services monitored:**
- sakin-ingest
- sakin-correlation
- sakin-soar
- sakin-panel-api
- sakin-clickhouse-sink
- sakin-baseline-worker
- Supporting infrastructure (ClickHouse, Kafka, Redis, PostgreSQL)

**Validation:**
```bash
promtool check config prometheus.yml
```

### Alertmanager (`alertmanager.yml`)

Alert routing, grouping, and notification delivery configuration.

**Features:**
- Severity-based routing (critical, warning)
- Slack integration for notifications
- Alert grouping and deduplication
- Configurable repeat intervals

**Environment variables required:**
- `SLACK_WEBHOOK_URL`: Slack incoming webhook URL

**Validation:**
```bash
amtool check-config alertmanager.yml
```

### Alert Rules (`alert-rules.yml`)

Prometheus recording and alerting rules covering:
- Service latency thresholds
- Kafka consumer lag monitoring
- Infrastructure health checks
- Resource utilization warnings

## Deployment

### Docker Compose

Services are configured in `../docker-compose.dev.yml`:

```yaml
prometheus:
  image: prom/prometheus:latest
  volumes:
    - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml
    - ./monitoring/alert-rules.yml:/etc/prometheus/rules/alert-rules.yml

grafana:
  image: grafana/grafana:latest
  environment:
    - GF_SECURITY_ADMIN_PASSWORD=admin

alertmanager:
  image: prom/alertmanager:latest
  volumes:
    - ./monitoring/alertmanager.yml:/etc/alertmanager/config.yml
  environment:
    - SLACK_WEBHOOK_URL=${SLACK_WEBHOOK_URL}

jaeger:
  image: jaegertracing/all-in-one:latest
  ports:
    - "16686:16686"  # UI
    - "4317:4317"    # OTLP receiver
```

### Kubernetes/Helm

Monitoring components can be deployed via Helm charts in `../k8s/helm/`:

```bash
helm install sakin-monitoring ./monitoring \
  --namespace monitoring \
  --values values-monitoring.yaml
```

## Usage

### Accessing Prometheus

```
http://localhost:9090
```

**Useful queries:**
- `rate(ingest_events_total[5m])` - Event ingestion rate
- `up{job="sakin-*"}` - Service health status
- `histogram_quantile(0.95, rate(request_duration_seconds_bucket[5m]))` - P95 latency

### Accessing Grafana

```
http://localhost:3000
Admin: admin / [password from .env]
```

Pre-configured dashboards:
1. **Alerts Overview** - Alert rates, severity distribution, top rules
2. **Playbook Execution** - Run counts, success rates, step duration
3. **Anomaly Detection** - Baseline vs actual, z-scores, anomalous entities
4. **System Health** - Latency histograms, Kafka lag, DB query times, agent status

### Accessing Alertmanager

```
http://localhost:9093
```

**Configuration:**
- Silencing alerts
- Managing alert groups
- Viewing active alerts
- Retry logic for failed notifications

### Accessing Jaeger (if enabled)

```
http://localhost:16686
```

**Features:**
- End-to-end trace visualization
- Latency analysis
- Error tracking
- Service dependency mapping

## Configuration

### Environment Variables

In `.env`:
- `SLACK_WEBHOOK_URL`: Required for Slack notifications
- `PROMETHEUS_PORT`: Port for Prometheus UI (default: 9090)
- `GRAFANA_ADMIN_PASSWORD`: Grafana admin password
- `JAEGER_UI_PORT`: Jaeger UI port (default: 16686)

### Customization

**Add custom alert rules:**
```yaml
- alert: CustomAlert
  expr: metric_name > threshold
  for: duration
  labels:
    severity: critical
  annotations:
    summary: "Alert summary"
    description: "Detailed description"
```

**Add custom Grafana datasource:**
```yaml
datasources:
  - name: MyDataSource
    type: prometheus
    url: http://prometheus:9090
```

## Troubleshooting

### Prometheus

**Issue**: Targets showing as "DOWN"
- Check service metrics endpoints are accessible
- Verify network connectivity
- Check Prometheus logs: `docker logs prometheus`

**Issue**: Rules not evaluating
- Validate PromQL syntax: `promtool check rules alert-rules.yml`
- Check rule evaluation in Prometheus UI under "Alerts"

### Grafana

**Issue**: Dashboards not loading data
- Verify Prometheus datasource connectivity
- Check query syntax in dashboard panels
- Review Grafana logs: `docker logs grafana`

**Issue**: Authentication failures
- Reset admin password via Grafana CLI
- Check user permissions and roles

### Alertmanager

**Issue**: Alerts not being sent
- Verify Slack webhook URL is valid
- Check Alertmanager logs: `docker logs alertmanager`
- Test webhook with: `curl -X POST <WEBHOOK_URL>`

### Jaeger

**Issue**: Traces not appearing
- Verify services have tracing enabled in config
- Check OTLP exporter configuration
- Verify network connectivity to Jaeger collector

## Performance Tuning

### Prometheus

- Increase `scrape_interval` for less frequent collection
- Increase `evaluation_interval` for rule evaluation frequency
- Configure retention policies: `--storage.tsdb.retention.time=30d`

### Grafana

- Enable caching for dashboard queries
- Optimize panel queries with appropriate time ranges
- Use recording rules for expensive queries

## Security

- **Prometheus**: Run behind reverse proxy with authentication
- **Grafana**: Change default admin password, enable RBAC
- **Alertmanager**: Restrict access to configuration endpoints
- **Jaeger**: Use network policies to restrict access

## Metrics Reference

### Application Metrics

**Ingestion:**
- `ingest_events_total` - Total events ingested
- `ingest_event_processing_duration_seconds` - Processing latency

**Correlation:**
- `correlation_alerts_total` - Total alerts generated
- `correlation_rule_evaluation_duration_seconds` - Rule evaluation time

**SOAR:**
- `soar_playbook_executions_total` - Total playbook runs
- `soar_playbook_success_total` - Successful playbook executions

**Panel API:**
- `panel_http_requests_total` - HTTP request count
- `panel_http_request_duration_seconds` - Response time

### Infrastructure Metrics

- `kafka_consumer_lag` - Kafka consumer lag per partition
- `redis_connected_clients` - Active Redis connections
- `postgres_up` - PostgreSQL availability
- `clickhouse_up` - ClickHouse availability

## Additional Resources

- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/grafana/latest/)
- [Alertmanager Documentation](https://prometheus.io/docs/alerting/latest/overview/)
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)
