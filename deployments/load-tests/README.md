# S.A.K.I.N. Performance & Load Testing Suite

Comprehensive K6-based load testing suite for validating S.A.K.I.N. platform performance under realistic and extreme load conditions.

## Overview

This test suite validates the platform's ability to handle:
- **10,000 Events Per Second (EPS)** ingestion across multiple collectors
- **Stateless & Stateful correlation rules** evaluation at scale
- **Complex analytics queries** against ClickHouse with pagination
- **SOAR playbook execution** with external integrations
- **Chaos scenarios** including database latency, cache failures, and broker issues

## Prerequisites

1. **K6 Installation**
   ```bash
   # macOS
   brew install k6

   # Linux (Ubuntu/Debian)
   sudo apt-get install k6

   # Or use Docker
   docker run --rm -i grafana/k6:latest run --vus 10 --duration 30s -
   ```

2. **S.A.K.I.N. Platform Running**
   - All services started via `docker-compose.dev.yml`
   - Postgres, Redis, Kafka, ClickHouse, Prometheus, Grafana, Jaeger healthy
   - Panel API and Correlation Engine endpoints accessible

3. **Network Connectivity**
   - All services on `sakin-network`
   - Load test machine can reach service endpoints
   - No network packet loss during tests

## Test Scripts

### 1. Ingestion Pipeline Tests (`ingestion-pipeline.js`)

Tests the entire ingestion path from collectors to Kafka.

**Scenarios:**
- **1k EPS baseline**: Basic ingestion performance
- **5k EPS sustained**: Extended load testing
- **10k EPS spike**: Peak load validation
- **Malformed data chaos**: 1% invalid events mixed in

**Collectors Simulated:**
- Windows EventLog (JSON format)
- CEF Syslog (text format)
- HTTP CEF Collector (JSON REST API)

**Metrics:**
- Ingestion latency: p50 < 50ms, p95 < 100ms, p99 < 150ms
- Kafka producer throughput
- Malformed event handling rate
- System resource utilization (CPU, memory, disk I/O)

**Running:**
```bash
# Baseline 1k EPS (100 VUs, 5 minutes)
k6 run ingestion-pipeline.js \
  --vus 100 \
  --duration 5m \
  -e TARGET_EPS=1000 \
  -e BASE_URL=http://localhost:8080

# High-load 10k EPS (1000 VUs, 5 minutes)
k6 run ingestion-pipeline.js \
  --vus 1000 \
  --duration 5m \
  -e TARGET_EPS=10000 \
  -e BASE_URL=http://localhost:8080
```

### 2. Correlation Engine Tests (`correlation-engine.js`)

Tests stateless and stateful rule evaluation.

**Scenarios:**
- **Stateless rules**: Evaluate 1000+ rules per event
- **SSH Brute-force (stateful)**: Aggregation window with 5-event threshold
- **Hot-key scenario**: Single source IP with 10k EPS to test lock contention
- **High-cardinality scenario**: 1M unique source IPs for memory pressure testing

**Metrics:**
- Rule evaluation latency: p50 < 30ms, p95 < 50ms, p99 < 100ms
- Alert creation rate
- Redis state store latency
- Memory usage (aggregation window)
- Lock contention events

**Running:**
```bash
# Normal scenario (balanced)
k6 run correlation-engine.js \
  --vus 100 \
  --duration 3m \
  -e TARGET_EPS=1000 \
  -e SCENARIO=normal \
  -e BASE_URL=http://localhost:8080

# Hot-key scenario (lock contention testing)
k6 run correlation-engine.js \
  --vus 1000 \
  --duration 3m \
  -e TARGET_EPS=10000 \
  -e SCENARIO=hot-key \
  -e BASE_URL=http://localhost:8080

# High-cardinality scenario (memory pressure)
k6 run correlation-engine.js \
  --vus 500 \
  --duration 3m \
  -e TARGET_EPS=5000 \
  -e SCENARIO=high-cardinality \
  -e BASE_URL=http://localhost:8080
```

### 3. Query Performance Tests (`query-performance.js`)

Tests alert list queries and analytics endpoints.

**Queries:**
- Alert list with pagination (1k, 10k records)
- Filtered queries (by severity, rule ID, time range)
- ClickHouse OLAP queries:
  - Top 10 source IPs by alert count (24h)
  - Alert distribution by severity (7 days)
  - User activity patterns
  - Data exfiltration detection
- Panel API endpoints (risk scores, lifecycle transitions, anomalies)

**Metrics:**
- Alert list latency: p50 < 100ms, p95 < 300ms, p99 < 500ms
- ClickHouse query latency: p95 < 500ms, p99 < 1s
- Panel API latency: p95 < 200ms, p99 < 500ms
- Cache hit rate

**Running:**
```bash
# Query performance baseline (50 VUs, 3 minutes)
k6 run query-performance.js \
  --vus 50 \
  --duration 3m \
  -e PANEL_API_URL=http://localhost:5000 \
  -e CLICKHOUSE_URL=http://localhost:8123

# High-load query testing (100 VUs)
k6 run query-performance.js \
  --vus 100 \
  --duration 3m \
  -e PANEL_API_URL=http://localhost:5000 \
  -e CLICKHOUSE_URL=http://localhost:8123
```

### 4. SOAR Playbook Execution Tests (`soar-playbook.js`)

Tests end-to-end playbook execution with external integrations.

**Playbooks:**
- **Block IP**: Firewall block + Slack notification + agent command
- **Create Jira Ticket**: Ticket creation + email notification
- **Collect Evidence**: Network logs, system logs collection

**External Integrations (Simulated):**
- Firewall (block action)
- Slack API
- Jira API
- Email service
- Agent commands (isolate network)

**Metrics:**
- Playbook execution latency: p50 < 500ms, p95 < 2s, p99 < 5s
- End-to-end latency: p95 < 3s, p99 < 5s
- Notification dispatch latency
- External API call latency
- Success rates

**Running:**
```bash
# SOAR playbook baseline (20 VUs, 2 minutes)
k6 run soar-playbook.js \
  --vus 20 \
  --duration 2m \
  -e SOAR_API_URL=http://localhost:8080

# High-load SOAR testing (50 VUs)
k6 run soar-playbook.js \
  --vus 50 \
  --duration 2m \
  -e SOAR_API_URL=http://localhost:8080
```

## Chaos Engineering Scenarios

### Scenario 1: Database Latency Injection

Simulate slow database responses:

```bash
# Using toxiproxy or tc (traffic control)
tc qdisc add dev docker0 root netem delay 3000ms

# Run ingestion tests
k6 run ingestion-pipeline.js --vus 100 --duration 2m

# Cleanup
tc qdisc del dev docker0 root
```

**Expectations:**
- Panel API responds with 504 Gateway Timeout
- Ingest and correlation (Kafka-based) unaffected
- No event loss

### Scenario 2: Redis Failure

Simulate cache outage:

```bash
# Stop Redis
docker stop sakin-redis

# Run correlation tests
k6 run correlation-engine.js --vus 100 --duration 2m

# Restart Redis
docker start sakin-redis

# Verify recovery
```

**Expectations:**
- Correlation engine circuit-breaker opens
- Graceful degradation (stateful aggregation disabled)
- No data loss in Kafka
- Recovery automatic upon Redis restart

### Scenario 3: Kafka Broker Failure

Simulate broker outage:

```bash
# Stop Kafka
docker stop sakin-kafka

# Run ingestion tests
k6 run ingestion-pipeline.js --vus 100 --duration 2m

# Monitor logs for retry behavior
docker logs sakin-ingest | grep -i "retry\|reconnect"

# Restart Kafka
docker start sakin-kafka

# Verify catchup
```

**Expectations:**
- Producer Polly retry policy activates (exponential backoff)
- No event loss (in-flight events retried)
- Automatic reconnection upon broker recovery
- Catch-up to current offset

### Scenario 4: Malformed Data Handling

Already built into ingestion tests (1% malformed rate).

**Verification:**
```bash
# Check logs
docker logs sakin-ingest | grep -i "parse\|error\|malformed"

# Verify good events still process
k6 run ingestion-pipeline.js --vus 100 --duration 1m
```

## Running All Tests

### Automated Test Suite

```bash
#!/bin/bash
set -e

echo "Starting S.A.K.I.N. Performance Test Suite..."

# 1. Baseline Ingestion (1k EPS)
echo "[1/4] Running ingestion baseline (1k EPS)..."
k6 run ingestion-pipeline.js --vus 100 --duration 5m \
  -e TARGET_EPS=1000 \
  --out json=results/ingestion-1k-eps.json

# 2. Correlation Engine (Normal)
echo "[2/4] Running correlation engine baseline..."
k6 run correlation-engine.js --vus 100 --duration 3m \
  -e TARGET_EPS=1000 \
  -e SCENARIO=normal \
  --out json=results/correlation-normal.json

# 3. Query Performance
echo "[3/4] Running query performance tests..."
k6 run query-performance.js --vus 50 --duration 3m \
  --out json=results/query-performance.json

# 4. SOAR Playbooks
echo "[4/4] Running SOAR playbook tests..."
k6 run soar-playbook.js --vus 20 --duration 2m \
  --out json=results/soar-playbook.json

echo "All tests completed! Results in ./results/"
```

### Docker-based Testing

```bash
# Run K6 test directly in Docker
docker run --rm \
  --network sakin-network \
  -v $(pwd)/deployments/load-tests:/scripts:ro \
  -v $(pwd)/results:/results \
  grafana/k6:latest run \
  --vus 100 \
  --duration 5m \
  /scripts/ingestion-pipeline.js
```

## Metrics & Monitoring

### Prometheus Scrape Config

K6 automatically exports metrics to Prometheus via:
```
http://prometheus:9090/api/v1/query
```

**Available Metrics:**
- `ingestion_latency_ms_*` (p50, p95, p99)
- `kafka_producer_throughput`
- `events_processed_total`
- `rule_eval_latency_ms_*`
- `alerts_created_total`
- `playbook_execution_latency_ms_*`

### Grafana Dashboards

Import dashboards:
- `monitoring/grafana/dashboards/performance-ingestion.json`
- `monitoring/grafana/dashboards/performance-correlation.json`
- `monitoring/grafana/dashboards/performance-queries.json`

### Jaeger Distributed Tracing

Each K6 request includes:
- `X-Trace-ID`: Unique trace identifier
- `X-Request-ID`: Request identifier

View traces at: `http://localhost:16686`

## Acceptance Criteria

✅ **Ingestion Pipeline**
- Latency p99 < 100ms @ 10k EPS
- No event loss (Kafka producer acknowledgment)
- Malformed data handling (no crash)
- CPU < 80%, Memory < 75%

✅ **Correlation Engine**
- Latency p99 < 50ms (rule evaluation)
- Stateful aggregation works correctly
- Hot-key scenario: no deadlocks (lock contention < 20%)
- High-cardinality scenario: memory stable (< 500MB)

✅ **Query Performance**
- Alert list p99 < 500ms (even 10k records)
- ClickHouse queries p99 < 1s
- Panel API p99 < 500ms
- Cache hit rate > 70%

✅ **SOAR Playbook**
- End-to-end latency p95 < 3s
- Playbook success rate > 95%
- Notification dispatch > 90% success
- Agent commands execute reliably

✅ **Chaos Resilience**
- DB latency: Panel API times out, other services unaffected
- Cache failure: Graceful degradation, no crash
- Broker failure: No event loss, automatic recovery
- Malformed data: Service continues, errors logged

## Output & Results

### JSON Summary
```bash
k6 run ingestion-pipeline.js \
  --out json=results/summary.json
```

### CSV Export
```bash
# Convert JSON to CSV for analysis
k6 run ingestion-pipeline.js \
  --out csv=results/summary.csv
```

### Console Summary
```
Checks..........................................: 95.50% ✓
Data received..................................: 425 MB
Data sent.......................................: 320 MB
http_req_blocked................................: avg=1.2ms
http_req_connecting............................: avg=0.8ms
http_req_duration...............................: avg=45ms
http_req_failed.................................: 0.50%
http_req_queued.................................avg=0.1ms
http_req_receiving..............................: avg=15ms
http_req_sending................................: avg=0.5ms
http_req_waiting................................: avg=29ms
```

## Troubleshooting

### Test Execution Issues

**K6 command not found**
```bash
# Add K6 to PATH or use full path
/usr/local/bin/k6 run ingestion-pipeline.js
```

**Connection refused**
```bash
# Verify services are running
docker-compose ps

# Check port accessibility
curl http://localhost:8080/healthz
```

**Out of memory / too many connections**
```bash
# Reduce VU count
k6 run ingestion-pipeline.js --vus 50 --duration 1m

# Increase system limits
ulimit -n 65536
```

### Service Issues During Tests

**High latency**
- Check Prometheus for resource utilization
- Verify disk I/O on database host
- Monitor Kafka consumer lag
- Check Redis eviction policy

**Events not processed**
- Verify Kafka topic exists: `kafka-topics --list --bootstrap-server localhost:9092`
- Check consumer group lag: `kafka-consumer-groups --bootstrap-server localhost:9092 --group correlation-engine --describe`
- Review service logs: `docker logs sakin-correlation`

**Query timeouts**
- Verify ClickHouse is responsive: `curl http://localhost:8123/ping`
- Check slow query log: `SELECT * FROM system.query_log WHERE duration_ms > 5000`
- Rebuild ClickHouse indexes if needed

## References

- **K6 Documentation**: https://k6.io/docs/
- **Prometheus Metrics**: https://prometheus.io/docs/
- **ClickHouse Performance**: https://clickhouse.com/docs/en/introduction/distinctive-features/
- **Kafka Performance Tuning**: https://kafka.apache.org/documentation/#bestpractices
- **Chaos Engineering**: https://principlesofchaos.org/

## Next Steps

1. Execute baseline ingestion test (1k EPS) and capture metrics
2. Run correlation engine test and verify stateful aggregation
3. Execute query performance suite
4. Run SOAR playbook tests
5. Execute chaos scenarios (DB latency, cache failure, broker failure)
6. Analyze results and document bottlenecks
7. Perform tuning based on findings
8. Re-run tests to verify improvements

---

**Last Updated**: Sprint 8
**Version**: 1.0
**Maintainer**: Performance Engineering Team
