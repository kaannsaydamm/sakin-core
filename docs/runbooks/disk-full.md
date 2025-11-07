# Runbook: Disk Full

## Overview

**Scenario**: Disk space exhausted on nodes or persistent volumes  
**Impact**: High - service degradation, write failures, data loss risk  
**RTO**: 30 minutes  
**Escalation**: L2 (SRE) immediately

## Symptoms

- Pods in CrashLoopBackOff or Error state
- "No space left on device" errors in logs
- Database write failures
- Kafka segment creation failures
- Alert: `NodeDiskPressure` or `PVCAlmostFull`

## Diagnosis

### Check Node Disk Usage

```bash
# Check all nodes
kubectl top nodes

# SSH to specific node (if accessible)
kubectl debug node/<node-name> -it --image=ubuntu
df -h

# Check which directories are large
du -sh /* | sort -h
```

### Check PVC Usage

```bash
# List all PVCs and their usage
kubectl get pvc -A

# Detailed usage for Postgres
kubectl exec -n sakin postgres-0 -- df -h /var/lib/postgresql/data

# Detailed usage for ClickHouse
kubectl exec -n sakin clickhouse-0 -- df -h /var/lib/clickhouse
```

### Identify Culprit

```bash
# Postgres: Check table sizes
kubectl exec -n sakin postgres-0 -- psql -U postgres -d sakin_db <<EOF
SELECT 
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC
LIMIT 10;
EOF

# ClickHouse: Check partition sizes
kubectl exec -n sakin clickhouse-0 -- clickhouse-client --query "
SELECT 
    table,
    partition,
    formatReadableSize(sum(bytes)) AS size
FROM system.parts
WHERE active
GROUP BY table, partition
ORDER BY sum(bytes) DESC
LIMIT 10;"

# Kafka: Check log segments
kubectl exec -n kafka kafka-0 -- du -sh /var/lib/kafka/data/* | sort -h
```

## Resolution Steps

### Scenario A: Database Too Large

#### Option 1: Archive Old Data

**Postgres**:
```bash
# Export old alerts to archive
kubectl exec -n sakin postgres-0 -- psql -U postgres -d sakin_db <<EOF
\COPY (SELECT * FROM alerts WHERE created_at < NOW() - INTERVAL '60 days') 
TO '/tmp/alerts_archive.csv' CSV HEADER;
EOF

# Copy archive to S3
kubectl cp sakin/postgres-0:/tmp/alerts_archive.csv ./alerts_archive.csv
aws s3 cp alerts_archive.csv s3://sakin-archives/alerts/$(date +%Y%m%d)/

# Delete archived data
kubectl exec -n sakin postgres-0 -- psql -U postgres -d sakin_db <<EOF
DELETE FROM alerts WHERE created_at < NOW() - INTERVAL '60 days';
VACUUM FULL alerts;
EOF
```

**ClickHouse**:
```bash
# Drop old partitions (TTL should handle this automatically)
kubectl exec -n sakin clickhouse-0 -- clickhouse-client --query "
ALTER TABLE sakin.events DROP PARTITION '202412';"
```

#### Option 2: Increase PVC Size

```bash
# Check if StorageClass supports expansion
kubectl get storageclass

# Edit PVC to increase size (if allowVolumeExpansion: true)
kubectl edit pvc -n sakin postgres-data-postgres-0

# Change spec.resources.requests.storage to larger value (e.g., 200Gi)

# Verify expansion
kubectl get pvc -n sakin postgres-data-postgres-0 --watch
```

### Scenario B: Log Files Filling Disk

```bash
# Find large log files
kubectl exec -n sakin correlation-deployment-xxx -- find /app/logs -type f -size +100M

# Rotate logs manually
kubectl exec -n sakin correlation-deployment-xxx -- sh -c "
    mv /app/logs/app.log /app/logs/app.log.old
    gzip /app/logs/app.log.old
"

# Or delete old logs (if rotation not working)
kubectl exec -n sakin correlation-deployment-xxx -- sh -c "
    find /app/logs -name '*.log.*' -mtime +7 -delete
"

# Long-term: Configure log rotation in Serilog
# Update appsettings.json:
{
  "Serilog": {
    "WriteTo": [{
      "Name": "File",
      "Args": {
        "path": "/app/logs/app.log",
        "rollingInterval": "Day",
        "retainedFileCountLimit": 7,
        "fileSizeLimitBytes": 104857600
      }
    }]
  }
}
```

### Scenario C: Kafka Logs Growing

```bash
# Check retention policy
kubectl exec -n kafka kafka-0 -- kafka-configs.sh \
    --bootstrap-server localhost:9092 \
    --entity-type topics \
    --entity-name normalized-events \
    --describe

# Reduce retention (temporarily)
kubectl exec -n kafka kafka-0 -- kafka-configs.sh \
    --bootstrap-server localhost:9092 \
    --entity-type topics \
    --entity-name normalized-events \
    --alter --add-config retention.ms=259200000  # 3 days

# Or delete old segments manually (emergency only)
kubectl exec -n kafka kafka-0 -- sh -c "
    find /var/lib/kafka/data -name '*.log' -mtime +7 -delete
"
```

### Scenario D: Docker Image Cache

```bash
# On Kubernetes nodes, clean up unused images
kubectl debug node/<node-name> -it --image=ubuntu
docker system prune -a --volumes --force

# Or use crictl (for containerd)
crictl rmi --prune
```

## Immediate Mitigation

If unable to free space quickly:

### 1. Emergency Read-Only Mode

```bash
# Set database to read-only (prevents writes)
kubectl exec -n sakin postgres-0 -- psql -U postgres -d sakin_db <<EOF
ALTER DATABASE sakin_db SET default_transaction_read_only = true;
EOF

# Notify users via status page
```

### 2. Stop Non-Critical Services

```bash
# Scale down analytics services temporarily
kubectl scale deployment -n sakin sakin-analytics-clickhouse-sink --replicas=0
kubectl scale deployment -n sakin sakin-analytics-baseline-worker --replicas=0

# This reduces write load
```

### 3. Emergency Storage Expansion

```bash
# If using cloud provider, expand volume at provider level
# AWS EBS
aws ec2 modify-volume --volume-id vol-xxxxx --size 200

# Then resize filesystem
kubectl exec -n sakin postgres-0 -- resize2fs /dev/xvdf
```

## Prevention

### Long-term Solutions

#### 1. Automated Cleanup Jobs

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: cleanup-old-alerts
spec:
  schedule: "0 4 * * *"  # Daily at 4 AM
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: cleanup
            image: postgres:15-alpine
            command:
            - psql
            - -c
            - DELETE FROM alerts WHERE created_at < NOW() - INTERVAL '90 days';
```

#### 2. Monitoring & Alerting

```yaml
# Prometheus alert rule
- alert: PVCAlmostFull
  expr: (kubelet_volume_stats_used_bytes / kubelet_volume_stats_capacity_bytes) > 0.80
  for: 5m
  annotations:
    summary: "PVC {{ $labels.persistentvolumeclaim }} is {{ $value | humanizePercentage }} full"
```

#### 3. Retention Policies

**Postgres**:
```sql
-- Create partition by month
CREATE TABLE alerts_2025_01 PARTITION OF alerts
    FOR VALUES FROM ('2025-01-01') TO ('2025-02-01');

-- Auto-drop old partitions
DROP TABLE IF EXISTS alerts_2024_10;
```

**ClickHouse**:
```sql
-- Ensure TTL is set
ALTER TABLE sakin.events MODIFY TTL event_date + INTERVAL 30 DAY;
```

**Kafka**:
```bash
# Set retention at topic level
kafka-configs.sh --bootstrap-server localhost:9092 \
    --entity-type topics --entity-name normalized-events \
    --alter --add-config retention.ms=604800000  # 7 days
```

#### 4. Disk Usage Dashboard

Create Grafana dashboard with:
- Node disk usage
- PVC usage per namespace
- Database table sizes
- Kafka log sizes

## Verification

```bash
# Check disk space recovered
kubectl exec -n sakin postgres-0 -- df -h

# Verify services healthy
kubectl get pods -n sakin
kubectl logs -n sakin deployment/sakin-correlation --tail=20

# Test write operations
curl -X POST https://sakin-api.company.com/api/test/event \
    -H "X-API-Key: your-key" \
    -d '{"type":"test"}'
```

## Related Runbooks

- [High Latency](./high-latency.md)
- [Memory Pressure](./memory-pressure.md)
- [Data Loss](./data-loss.md)

## Contacts

- **L1 On-Call**: Platform team
- **L2 Escalation**: SRE/Infrastructure team
