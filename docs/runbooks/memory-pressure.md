# Runbook: Memory Pressure / OOM Killer

## Overview

**Scenario**: Service consuming excessive memory or terminated by OOM killer  
**Impact**: Medium-High - service restarts, processing delays  
**RTO**: 15 minutes  
**Escalation**: L2 (SRE) if repeated OOM events

## Symptoms

- Pods restarting frequently (CrashLoopBackOff)
- Exit code 137 in pod events (OOM killed)
- Alert: `PodMemoryUsageHigh` or `ContainerOOMKilled`
- Slow query performance
- API timeouts

## Diagnosis

### Check Pod Memory Usage

```bash
# Current memory usage
kubectl top pods -n sakin --sort-by=memory

# Detailed pod metrics
kubectl describe pod -n sakin <pod-name> | grep -A 10 "Limits\|Requests"

# Check OOM kills
kubectl get events -n sakin --field-selector reason=OOMKilling

# Pod logs before crash
kubectl logs -n sakin <pod-name> --previous --tail=100
```

### Identify Memory Leak

```bash
# Check if memory steadily increasing
kubectl top pod -n sakin <pod-name> --containers
# Run multiple times, observe trend

# For .NET apps, check GC metrics
kubectl exec -n sakin <pod-name> -- dotnet-counters ps
kubectl exec -n sakin <pod-name> -- dotnet-counters monitor --process-id <pid>

# Heap size, GC pauses, allocation rate
```

### Check Service-Specific Issues

**Correlation Engine**:
```bash
# Check rule cache size
kubectl exec -n sakin deployment/sakin-correlation -- \
    curl -s http://localhost:8080/metrics | grep sakin_rule_cache

# Check alert deduplication cache
kubectl exec -n sakin deployment/sakin-correlation -- \
    curl -s http://localhost:8080/metrics | grep sakin_dedup_cache
```

**Redis**:
```bash
# Check Redis memory
kubectl exec -n sakin deploy/redis -- redis-cli INFO memory

# Check key count
kubectl exec -n sakin deploy/redis -- redis-cli INFO keyspace

# Find large keys
kubectl exec -n sakin deploy/redis -- redis-cli --bigkeys
```

**ClickHouse**:
```bash
# Check query memory usage
kubectl exec -n sakin clickhouse-0 -- clickhouse-client --query "
SELECT 
    query,
    memory_usage,
    formatReadableSize(memory_usage) AS memory
FROM system.processes
ORDER BY memory_usage DESC;"
```

## Resolution Steps

### Scenario A: Memory Limit Too Low

If service legitimately needs more memory:

```bash
# Increase memory limit
kubectl edit deployment -n sakin sakin-correlation

# Update resources:
spec:
  template:
    spec:
      containers:
      - name: correlation
        resources:
          requests:
            memory: "1Gi"
          limits:
            memory: "2Gi"  # Increased from 1Gi

# Apply changes
kubectl rollout status deployment/sakin-correlation -n sakin
```

### Scenario B: Memory Leak in Application

#### .NET Memory Leak

```bash
# Take memory dump for analysis
kubectl exec -n sakin <pod-name> -- dotnet-gcdump collect -p <pid>

# Copy dump locally
kubectl cp sakin/<pod-name>:/tmp/gcdump_xxx.gcdump ./gcdump.gcdump

# Analyze with dotnet-gcdump or Visual Studio
dotnet-gcdump report gcdump.gcdump
```

**Common .NET Memory Leak Causes**:
1. Event handlers not unsubscribed
2. Static collections growing unbounded
3. Cache not evicting old entries
4. Large object heap fragmentation

**Fixes**:
```csharp
// Fix 1: Dispose event subscriptions
public void Dispose()
{
    _eventBus.OnEvent -= HandleEvent;
}

// Fix 2: Bounded cache
var cacheOptions = new MemoryCacheOptions
{
    SizeLimit = 10000,  // Max entries
    ExpirationScanFrequency = TimeSpan.FromMinutes(5)
};

// Fix 3: Use weak references for caches
WeakReference<object> weakRef = new WeakReference<object>(largeObject);
```

### Scenario C: Redis Memory Exhaustion

```bash
# Check current memory usage
kubectl exec -n sakin deploy/redis -- redis-cli INFO memory
# Look for: used_memory_human, maxmemory, evicted_keys

# Option 1: Increase maxmemory
kubectl exec -n sakin deploy/redis -- redis-cli CONFIG SET maxmemory 4gb

# Option 2: Enable eviction policy
kubectl exec -n sakin deploy/redis -- redis-cli CONFIG SET maxmemory-policy allkeys-lru

# Option 3: Clear stale keys
kubectl exec -n sakin deploy/redis -- redis-cli --scan --pattern "dedup:*" | \
    xargs -n 100 kubectl exec -n sakin deploy/redis -- redis-cli DEL

# Option 4: Flush cache (if safe)
kubectl exec -n sakin deploy/redis -- redis-cli FLUSHDB
```

### Scenario D: Database Connection Pool Leak

```bash
# Check active connections
kubectl exec -n sakin postgres-0 -- psql -U postgres -c "
SELECT count(*), state 
FROM pg_stat_activity 
GROUP BY state;"

# Find long-running queries
kubectl exec -n sakin postgres-0 -- psql -U postgres -c "
SELECT pid, usename, application_name, state, query
FROM pg_stat_activity 
WHERE state = 'active' AND query_start < NOW() - INTERVAL '1 minute';"

# Kill stuck connections (if needed)
kubectl exec -n sakin postgres-0 -- psql -U postgres -c "
SELECT pg_terminate_backend(pid) 
FROM pg_stat_activity 
WHERE state = 'idle in transaction' 
  AND query_start < NOW() - INTERVAL '10 minutes';"
```

**Fix in Code**:
```csharp
// Ensure connection disposal
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
// ... use connection
// Automatically disposed
```

### Scenario E: Large Query Results

If service loads too much data into memory:

```bash
# Check API response sizes
kubectl logs -n sakin deployment/sakin-panel-api | \
    grep "Response size" | awk '{sum+=$NF} END {print sum/NR}'
```

**Fixes**:
1. Implement pagination
2. Use streaming responses
3. Add result size limits

```csharp
// Pagination
[HttpGet("alerts")]
public async Task<IActionResult> GetAlerts(
    [FromQuery] int page = 1, 
    [FromQuery] int pageSize = 100)
{
    if (pageSize > 1000) pageSize = 1000;  // Max limit
    
    var alerts = await _db.Alerts
        .OrderByDescending(a => a.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();
    
    return Ok(alerts);
}
```

## Immediate Mitigation

### 1. Restart Service

```bash
# Quick restart to reclaim memory
kubectl rollout restart deployment/sakin-correlation -n sakin

# Or delete pod to force recreation
kubectl delete pod -n sakin <pod-name>
```

### 2. Scale Horizontally

```bash
# Add more replicas to distribute load
kubectl scale deployment/sakin-correlation -n sakin --replicas=5

# Memory used per pod decreases
```

### 3. Reduce Load

```bash
# Temporarily disable non-critical rules
kubectl exec -n sakin deploy/sakin-panel-api -- \
    psql -U postgres -d sakin_db \
    -c "UPDATE rules SET enabled = false WHERE severity = 'low';"

# Reduce Kafka batch size (less memory per batch)
# Update ConfigMap with smaller BatchSize
```

## Prevention

### 1. Set Appropriate Limits

```yaml
resources:
  requests:
    memory: "512Mi"  # Guaranteed
  limits:
    memory: "2Gi"    # Max (2x-4x requests)
```

**Guidelines**:
- Correlation: 1-2 GB
- Panel API: 512 MB - 1 GB
- Analytics: 2-4 GB
- SOAR: 512 MB

### 2. Configure Memory Limits in Application

**.NET GC Settings**:
```xml
<PropertyGroup>
  <ServerGarbageCollection>true</ServerGarbageCollection>
  <GarbageCollectionAdaptationMode>1</GarbageCollectionAdaptationMode>
  <GCHeapHardLimit>1800000000</GCHeapHardLimit>  <!-- 1.8 GB -->
</PropertyGroup>
```

### 3. Implement Circuit Breakers

```csharp
// Polly circuit breaker to prevent memory exhaustion
var circuitBreaker = Policy
    .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
    .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1));
```

### 4. Monitoring & Alerts

```yaml
# Prometheus alert
- alert: PodMemoryUsageHigh
  expr: |
    (container_memory_working_set_bytes{namespace="sakin"} 
    / container_spec_memory_limit_bytes{namespace="sakin"}) > 0.85
  for: 5m
  annotations:
    summary: "Pod {{ $labels.pod }} memory usage > 85%"
```

### 5. Memory Profiling in CI/CD

```bash
# Add memory test to CI pipeline
dotnet test --collect:"XPlat Code Coverage" --settings runsettings.xml

# Fail if memory usage exceeds threshold
if [ $MEMORY_USAGE -gt $THRESHOLD ]; then
    echo "Memory usage too high: $MEMORY_USAGE MB"
    exit 1
fi
```

## Troubleshooting Tips

### .NET Memory Analysis

```bash
# Attach dotnet-trace to running process
kubectl exec -n sakin <pod-name> -it -- bash
dotnet-trace collect -p $(pidof dotnet) --profile gc-collect

# Download trace file
kubectl cp sakin/<pod-name>:/tmp/trace.nettrace ./trace.nettrace

# Analyze with PerfView or dotnet-trace
dotnet-trace report trace.nettrace
```

### Redis Memory Analysis

```bash
# Sample keys to find large ones
kubectl exec -n sakin deploy/redis -- redis-cli --bigkeys

# Get memory usage per key
kubectl exec -n sakin deploy/redis -- redis-cli MEMORY USAGE <key>

# Check fragmentation
kubectl exec -n sakin deploy/redis -- redis-cli INFO memory | grep fragmentation
```

## Verification

```bash
# Monitor memory after fix
watch -n 5 'kubectl top pods -n sakin --sort-by=memory'

# Check no OOM events
kubectl get events -n sakin --field-selector reason=OOMKilling

# Verify service healthy
kubectl get pods -n sakin
curl https://sakin-api.company.com/health
```

## Related Runbooks

- [High Latency](./high-latency.md)
- [Alert Storm](./alert-storm.md)
- [Disk Full](./disk-full.md)

## Contacts

- **L1 On-Call**: Platform team
- **L2 Escalation**: SRE/Performance team
