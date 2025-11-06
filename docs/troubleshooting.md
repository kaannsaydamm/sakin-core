# Troubleshooting Guide

## Common Issues & Solutions

### Ingestion Issues

#### 1. Events Not Being Ingested

**Symptoms:** No events appearing in Kafka, slow ingestion rate

**Diagnosis:**
```bash
# Check Kafka topics exist
docker exec kafka kafka-topics --list --bootstrap-server kafka:9092

# Check consumer group status
docker exec kafka kafka-consumer-groups \
  --bootstrap-server kafka:9092 \
  --group ingest-service \
  --describe

# View raw events topic
docker exec kafka kafka-console-consumer \
  --bootstrap-server kafka:9092 \
  --topic raw-events \
  --from-beginning \
  --max-messages 5
```

**Solutions:**
```bash
# Create missing topics
docker exec kafka kafka-topics \
  --create --topic raw-events \
  --bootstrap-server kafka:9092 \
  --partitions 1 --replication-factor 1

# Check ingest service logs
docker logs -f ingest

# Verify network connectivity
docker exec ingest ping kafka

# Check Kafka broker configuration
docker exec kafka kafka-configs \
  --bootstrap-server kafka:9092 \
  --describe --entity-type brokers --entity-name 1
```

#### 2. Parser Errors

**Symptoms:** High error rate in logs, events not normalized

**Diagnosis:**
```bash
# Monitor parsing errors
curl "http://localhost:9090/api/v1/query?query=rate(sakin_ingest_parsing_errors_total[5m])"

# Check specific parser logs
docker logs ingest | grep "ParseError"

# Test parser manually
curl -X POST http://localhost:5000/api/test/parse \
  -H "Content-Type: application/json" \
  -d '{"format": "syslog", "raw": "<34>Oct 11 22:14:15 hostname kernel"}'
```

**Solutions:**
```bash
# Update parser configuration
# Edit sakin-ingest/appsettings.json:
{
  "Parsers": {
    "Syslog": {
      "Enabled": true,
      "StrictParsing": false  # More lenient
    }
  }
}

# Add custom field mapping
{
  "FieldMappings": {
    "sourceIp": "custom_src_field",
    "destIp": "custom_dst_field"
  }
}

# Restart service
docker restart ingest
```

#### 3. GeoIP Enrichment Not Working

**Symptoms:** Missing geo fields in enriched events

**Diagnosis:**
```bash
# Check GeoIP database
ls -la data/geoip/

# Test GeoIP lookup
curl -X POST http://localhost:5000/api/test/geoip \
  -H "Content-Type: application/json" \
  -d '{"ip": "8.8.8.8"}'

# Check if configured
curl http://localhost:5000/api/config | jq .geoip
```

**Solutions:**
```bash
# Download GeoIP database
mkdir -p data/geoip
# Download from: https://dev.maxmind.com/geoip/geolite2-open-data-locations/

# Update configuration
{
  "GeoIp": {
    "Enabled": true,
    "DatabasePath": "/data/geoip/GeoLite2-City.mmdb",
    "CacheTtlSeconds": 3600
  }
}

# Restart and test
docker restart ingest
```

### Correlation Issues

#### 1. Rules Not Triggering

**Symptoms:** Alerts not generated despite matching events

**Diagnosis:**
```bash
# Verify rules loaded
curl http://localhost:5000/api/rules | jq .

# Check specific rule
curl http://localhost:5000/api/rules/rule-id

# Monitor rule evaluation
docker logs -f correlation | grep "Evaluating rule-id"

# Test rule with sample event
curl -X POST http://localhost:5000/api/test/rule \
  -H "Content-Type: application/json" \
  -d '{
    "ruleId": "rule-id",
    "event": {
      "EventType": "AuthenticationFailure",
      "SourceIp": "192.168.1.100"
    }
  }'
```

**Solutions:**
```bash
# Verify rule syntax
jq empty rule-file.json

# Check rule is enabled
# Edit rule JSON: "Enabled": true

# Reload rules
curl -X POST http://localhost:5000/api/rules/reload

# Check conditions match event fields
# Verify Field names are correct

# Review logs for evaluation errors
docker logs correlation | tail -100
```

#### 2. High Redis Memory Usage

**Symptoms:** OOM errors, slow performance, evicted keys

**Diagnosis:**
```bash
# Check Redis memory
redis-cli INFO memory

# Monitor key statistics
redis-cli INFO stats

# List large keys
redis-cli --bigkeys

# Check used memory percentage
redis-cli INFO | grep used_memory_human
```

**Solutions:**
```bash
# Increase Redis memory limit
# In docker-compose.yml:
redis:
  command: redis-server --maxmemory 2gb --maxmemory-policy allkeys-lru

# Reduce stateful rule window
# Edit rule: "WindowSeconds": 300  # Instead of 3600

# Increase TTL cleanup interval
{
  "RuleEngine": {
    "StatefulWindowCleanupIntervalSeconds": 1800  # More frequent
  }
}

# Restart services
docker restart redis correlation
```

#### 3. Duplicate Alerts

**Symptoms:** Same alert generated multiple times

**Diagnosis:**
```bash
# Check deduplication configuration
curl http://localhost:5000/api/config/dedup

# Monitor dedup metrics
curl "http://localhost:9090/api/v1/query?query=sakin_alert_dedup_ratio"

# Check Redis for dedup keys
redis-cli KEYS "sakin:dedup:*" | head -20
```

**Solutions:**
```bash
# Adjust dedup window
{
  "Deduplication": {
    "WindowSeconds": 600,  # Increase window
    "Enabled": true
  }
}

# Clear existing dedup state (careful!)
redis-cli FLUSHDB

# Adjust dedup fields
{
  "Deduplication": {
    "FieldsToMatch": ["RuleId", "SourceIp", "Hostname"]
  }
}

# Restart correlation
docker restart correlation
```

### SOAR Execution Issues

#### 1. Playbooks Not Executing

**Symptoms:** Alerts generated but no playbook actions

**Diagnosis:**
```bash
# Check playbooks loaded
curl http://localhost:5000/api/playbooks | jq .

# Monitor playbook execution
docker logs -f soar | grep "Executing playbook"

# Check trigger rules
curl http://localhost:5000/api/playbooks/id | jq .triggerRules

# Verify alerts reaching SOAR
docker logs soar | grep "Alert received"
```

**Solutions:**
```bash
# Verify trigger rules match alert rule
# Playbook: "triggerRules": ["rule-brute-force"]
# Must match actual rule ID

# Enable playbook
# Edit playbook JSON: "Enabled": true

# Reload playbooks
curl -X POST http://localhost:5000/api/playbooks/reload

# Check Kafka connectivity
docker logs soar | grep "Kafka.*error"

# Verify topic configuration
# Playbook should consume from "alerts" topic
```

#### 2. Notification Delivery Failures

**Symptoms:** Playbook runs but notifications not received

**Diagnosis:**
```bash
# Check notification configuration
curl http://localhost:5000/api/config/notifications | jq .

# Monitor failed notifications
docker logs soar | grep "Notification.*failed"

# Test external service connectivity
# For Slack:
curl -X POST "$SLACK_WEBHOOK_URL" \
  -H 'Content-type: application/json' \
  -d '{"text":"Test message"}'

# For Email:
telnet smtp.gmail.com 587

# For Jira:
curl -u bot:token https://jira.company.com/rest/api/3/myself
```

**Solutions:**
```bash
# Verify credentials
{
  "Notifications": {
    "Slack": {
      "WebhookUrl": "https://hooks.slack.com/...",
      "Enabled": true
    },
    "Email": {
      "SmtpServer": "smtp.gmail.com",
      "SmtpPort": 587,
      "Username": "your-email@gmail.com",
      "Enabled": true
    }
  }
}

# Test notification step
curl -X POST http://localhost:5000/api/test/notification \
  -d '{
    "channel": "slack",
    "message": "Test alert"
  }'

# Check firewall/network policies
# Allow outbound to notification services

# Increase retry count
{
  "Notifications": {
    "RetryCount": 3,
    "RetryDelayMs": 1000
  }
}
```

#### 3. Agent Command Failures

**Symptoms:** Commands not executed, timeouts, errors

**Diagnosis:**
```bash
# List registered agents
curl http://localhost:5000/api/agents | jq .

# Check agent connectivity
curl http://localhost:5000/api/agents/{id}/health

# Monitor command execution
docker logs soar | grep "AgentCommand.*error"

# Check command logs on agent
# SSH to agent: tail -f /var/log/sakin-agent.log
```

**Solutions:**
```bash
# Verify agent is running
docker ps | grep agent

# Check agent registration
{
  "Agents": {
    "Agents": [
      {
        "Id": "firewall-01",
        "Endpoint": "https://firewall.internal:9000",
        "Certificate": "/certs/agent.crt"
      }
    ]
  }
}

# Increase command timeout
{
  "Agents": {
    "Timeout": 30000,  # milliseconds
    "RetryCount": 3
  }
}

# Test agent connectivity
curl --cert /certs/client.crt \
     --key /certs/client.key \
     https://firewall.internal:9000/healthz

# Check mTLS certificates
openssl x509 -in /certs/agent.crt -text -noout
```

### Performance Issues

#### 1. High Latency

**Symptoms:** Slow alert generation, long query times

**Diagnosis:**
```bash
# Check service latencies
curl "http://localhost:9090/api/v1/query?query=histogram_quantile(0.99,rate(sakin_request_duration_seconds_bucket[5m]))"

# Monitor CPU/Memory
docker stats correlation ingest soar

# Check Kafka consumer lag
docker exec kafka kafka-consumer-groups \
  --bootstrap-server kafka:9092 \
  --describe

# Profile hotspots
docker exec correlation /bin/bash -c "dotnet-trace collect -p 1 -o trace.nettrace"
```

**Solutions:**
```bash
# Scale up affected service
docker compose up -d --scale correlation=3

# Increase resource limits
# Edit service in docker-compose.yml:
deploy:
  resources:
    limits:
      cpus: '2'
      memory: 4G

# Optimize database queries
# Add indexes to frequently queried fields
CREATE INDEX idx_alerts_status ON alerts(status);
CREATE INDEX idx_alerts_timestamp ON alerts(timestamp DESC);

# Batch Kafka messages
{
  "Kafka": {
    "BatchSize": 100,
    "BatchTimeoutMs": 5000
  }
}

# Restart services
docker compose restart
```

#### 2. High Memory Usage

**Symptoms:** OOM kills, memory pressure alerts

**Diagnosis:**
```bash
# Check memory per service
docker stats --no-stream

# Monitor memory trend
docker stats correlation --no-stream | watch

# Check for memory leaks
docker logs correlation | grep -i "gc\|memory\|dispose"

# Profile memory usage
dotnet trace collect --duration 60s
```

**Solutions:**
```bash
# Reduce cache sizes
{
  "Caching": {
    "MaxSize": 50000,  # Instead of 100000
    "TtlSeconds": 300
  }
}

# Increase GC frequency
DOTNET_GCHeapCount=4
DOTNET_GCHeapAffinitizeMask=0xf

# Add swap (temporary measure)
docker update --memory-swap 4G correlation

# Restart with lower memory
docker update --memory 2G correlation
```

### Database Issues

#### 1. PostgreSQL Connection Pool Exhausted

**Symptoms:** "Connection pool exhausted", slow queries

**Diagnosis:**
```bash
# Check connection count
psql -c "SELECT count(*) FROM pg_stat_activity;"

# View active connections
psql -c "SELECT pid, usename, state FROM pg_stat_activity;"

# Check pool configuration
psql -c "SHOW max_connections;"
```

**Solutions:**
```bash
# Increase connection limit
{
  "Database": {
    "MaxPoolSize": 20
  }
}

# Close idle connections
ALTER SYSTEM SET idle_in_transaction_session_timeout = '5min';

# Optimize slow queries
EXPLAIN ANALYZE SELECT * FROM alerts WHERE status = 'New';

# Add indexes
CREATE INDEX idx_alerts_status ON alerts(status);

# Restart PostgreSQL
docker restart postgres
```

#### 2. Slow Alert Queries

**Symptoms:** API timeouts, slow dashboard

**Diagnosis:**
```bash
# Identify slow queries
psql -c "SELECT query, mean_time FROM pg_stat_statements ORDER BY mean_time DESC LIMIT 10;"

# Check query plan
psql -c "EXPLAIN ANALYZE SELECT * FROM alerts WHERE status='New';"

# Monitor query performance
docker exec postgres pg_stat_monitor
```

**Solutions:**
```bash
# Add composite indexes
CREATE INDEX idx_alerts_status_timestamp ON alerts(status, timestamp DESC);

# Partition large tables
CREATE TABLE alerts_new PARTITION OF alerts
  FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');

# Archive old data
DELETE FROM alerts WHERE created_at < now() - interval '90 days';

# Update statistics
ANALYZE alerts;
VACUUM alerts;
```

## Debug Mode

### Enable Verbose Logging

```bash
# Set environment variables
ASPNETCORE_LOGLEVEL=Debug
Serilog__MinimumLevel=Debug

# Restart services
docker compose restart
```

### Enable Tracing

```bash
# View Jaeger traces
# http://localhost:16686

# Export traces
docker exec jaeger jaeger-all-in-one export --trace.memory.max-traces=1000

# Query specific traces
curl "http://localhost:16686/api/traces?service=sakin-correlation"
```

### Health Check Endpoint

```bash
# Check service health
curl http://localhost:5000/healthz

# Check specific dependency
curl http://localhost:5000/healthz?check=database
curl http://localhost:5000/healthz?check=kafka
curl http://localhost:5000/healthz?check=redis
```

## Getting Support

### Gather Diagnostic Information

```bash
# Collect logs from all services
docker compose logs > logs.txt

# Export metrics
curl "http://localhost:9090/api/v1/query?query={__name__=~'sakin_.*'}" > metrics.json

# Export service status
docker compose ps > status.txt

# System information
uname -a > system.txt
free -h >> system.txt
df -h >> system.txt
```

### Create Issue with Diagnostics

When reporting issues, include:
1. Error messages and logs
2. Configuration (sanitized)
3. Metrics or performance data
4. Steps to reproduce
5. Expected vs actual behavior

### Contact Support

- **GitHub Issues**: https://github.com/kaannsaydamm/sakin-core/issues
- **GitHub Discussions**: https://github.com/kaannsaydamm/sakin-core/discussions
- **Email**: [support contact]

---

**See Also:**
- [Monitoring Guide](./monitoring.md)
- [Architecture Overview](./architecture.md)
- [Deployment Guide](./deployment.md)
