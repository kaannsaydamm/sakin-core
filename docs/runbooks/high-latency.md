# Runbook: High Latency

## Overview

**Scenario**: API or processing latency exceeds acceptable thresholds  
**Impact**: Medium-High - delayed alerts, slow UI, user complaints  
**RTO**: 30 minutes  
**Escalation**: L2 (SRE) if unresolved in 30 minutes

## Symptoms

- API response time > 2 seconds (p95)
- Alert processing delay > 10 seconds
- Grafana dashboard showing red latency metrics
- User reports: "Dashboard is slow" or "Alerts delayed"
- Prometheus alerts firing: `SakinHighLatency`

## Diagnosis

### 1. Identify Affected Component

```bash
# Check service latencies
kubectl top pods -n sakin

# Check Prometheus metrics
# Panel API latency
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket{service="sakin-panel-api"}[5m]))

# Correlation latency
histogram_quantile(0.95, rate(sakin_correlation_latency_seconds_bucket[5m]))

# Database query time
pg_stat_statements_mean_exec_time
```

### 2. Check Database Performance

```bash
# Connect to Postgres
kubectl exec -n sakin deploy/postgres -it -- psql -U postgres -d sakin_db

# Check slow queries
SELECT 
    query,
    calls,
    mean_exec_time,
    total_exec_time
FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 10;

# Check active connections
SELECT count(*) FROM pg_stat_activity WHERE state = 'active';

# Check locks
SELECT * FROM pg_locks WHERE NOT granted;
```

### 3. Check Redis Performance

```bash
# Connect to Redis
kubectl exec -n sakin deploy/redis -it -- redis-cli

# Check latency
> INFO stats
> SLOWLOG GET 10

# Check memory usage
> INFO memory

# Check connected clients
> CLIENT LIST
```

### 4. Check ClickHouse Performance

```bash
# Connect to ClickHouse
kubectl exec -n sakin deploy/clickhouse -it -- clickhouse-client

# Check running queries
SELECT * FROM system.processes;

# Check slow queries
SELECT 
    query,
    query_duration_ms,
    read_rows,
    memory_usage
FROM system.query_log
WHERE type = 'QueryFinish'
  AND event_time > now() - INTERVAL 5 MINUTE
ORDER BY query_duration_ms DESC
LIMIT 10;
```

## Resolution Steps

### Scenario A: Slow Database Queries

**Symptoms**: High `pg_stat_statements_mean_exec_time`, slow API responses

**Resolution**:

1. **Identify slow query**:
   ```sql
   SELECT query, mean_exec_time 
   FROM pg_stat_statements 
   ORDER BY mean_exec_time DESC LIMIT 1;
   ```

2. **Check if index missing**:
   ```sql
   EXPLAIN ANALYZE <slow_query>;
   ```

3. **Add index if needed** (coordinate with team first):
   ```sql
   CREATE INDEX CONCURRENTLY idx_alerts_timestamp 
   ON alerts(created_at DESC);
   ```

4. **Increase connection pool** (if `max_connections` reached):
   ```bash
   # Update Postgres config
   kubectl edit configmap -n sakin postgres-config
   # Set max_connections = 200
   
   # Restart Postgres
   kubectl rollout restart statefulset/postgres -n sakin
   ```

### Scenario B: Redis Memory Pressure

**Symptoms**: Redis slow, high memory usage, evictions occurring

**Resolution**:

1. **Check memory**:
   ```bash
   redis-cli INFO memory
   # Look for used_memory_human, maxmemory, evicted_keys
   ```

2. **Increase Redis memory limit**:
   ```bash
   kubectl edit deployment/redis -n sakin
   # Increase memory limit in resources section
   ```

3. **Clear stale keys** (if dedup cache too large):
   ```bash
   redis-cli --scan --pattern "dedup:*" | head -1000 | xargs redis-cli DEL
   ```

4. **Consider Redis Cluster** for horizontal scaling

### Scenario C: ClickHouse Query Overload

**Symptoms**: Slow anomaly detection queries, high ClickHouse CPU

**Resolution**:

1. **Check query performance**:
   ```sql
   SELECT query, query_duration_ms 
   FROM system.query_log 
   WHERE query_duration_ms > 5000 
   ORDER BY event_time DESC LIMIT 5;
   ```

2. **Add/verify indexes**:
   ```sql
   -- Check if bloom filter indexes exist
   SHOW CREATE TABLE sakin.events;
   
   -- Add if missing (in schema migration)
   ALTER TABLE sakin.events ADD INDEX idx_username username TYPE bloom_filter();
   ```

3. **Optimize partition pruning**:
   - Ensure queries include `event_date` filter
   - Check partition key in queries

4. **Scale ClickHouse** (if consistently overloaded):
   ```bash
   # Add more replicas
   kubectl scale statefulset/clickhouse -n sakin --replicas=3
   ```

### Scenario D: Kafka Consumer Lag

**Symptoms**: Events processing slowly, high consumer lag

**Resolution**:

1. **Check lag**:
   ```bash
   kafka-consumer-groups.sh --bootstrap-server kafka:9092 \
       --group sakin-correlation --describe
   ```

2. **Scale correlation workers**:
   ```bash
   kubectl scale deployment/sakin-correlation -n sakin --replicas=5
   ```

3. **Increase batch size** (if CPU not bottleneck):
   ```json
   {
     "Kafka": {
       "Consumer": {
         "BatchSize": 500,
         "BatchTimeoutMs": 1000
       }
     }
   }
   ```

### Scenario E: Network Latency

**Symptoms**: High latency between services, cross-AZ traffic

**Resolution**:

1. **Check inter-service latency**:
   ```bash
   # From panel-api to correlation
   kubectl exec -n sakin deploy/sakin-panel-api -- \
       curl -w "@curl-format.txt" http://sakin-correlation:8080/health
   ```

2. **Enable service mesh** (Istio/Linkerd) for better routing

3. **Co-locate services** in same AZ if possible

## Immediate Mitigation

If unable to resolve quickly:

1. **Cache aggressively**:
   ```csharp
   // In Panel API, increase cache TTL
   _cache.Set(key, value, TimeSpan.FromMinutes(10));
   ```

2. **Reduce query complexity**:
   - Limit result sets
   - Add pagination
   - Use materialized views

3. **Shed load**:
   - Return 503 for non-critical endpoints
   - Implement rate limiting

## Prevention

### Long-term Solutions

1. **Query optimization**:
   - Regular EXPLAIN ANALYZE on slow queries
   - Add missing indexes
   - Denormalize hot paths

2. **Caching strategy**:
   - Cache expensive queries
   - Use Redis for hot data
   - Implement CDN for static assets

3. **Database tuning**:
   ```sql
   -- Postgres performance tuning
   ALTER SYSTEM SET shared_buffers = '4GB';
   ALTER SYSTEM SET effective_cache_size = '12GB';
   ALTER SYSTEM SET work_mem = '64MB';
   ```

4. **Monitoring**:
   - Set up Grafana dashboards for latency
   - Alert on p95 > 2s, p99 > 5s
   - Track slow query trends

5. **Load testing**:
   - Regular performance testing
   - Identify bottlenecks before production
   - Capacity planning

## Verification

After resolution:

```bash
# Check latency improved
watch -n 5 'curl -w "@curl-format.txt" https://sakin-api/health'

# Verify Prometheus metrics
# p95 latency should be < 2s

# Check user reports cleared
```

## Related Runbooks

- [Alert Storm](./alert-storm.md)
- [Disk Full](./disk-full.md)
- [Memory Pressure](./memory-pressure.md)

## Contacts

- **L1 On-Call**: Platform team
- **L2 Escalation**: SRE/Database team
