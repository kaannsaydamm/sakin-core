# Runbook: Alert Storm

## Overview

**Scenario**: System experiencing high alert volume (>10,000 alerts/minute)  
**Impact**: High - correlation engine overload, delayed alert processing  
**RTO**: 15 minutes  
**Escalation**: L2 (SRE) if unresolved in 15 minutes

## Symptoms

- Prometheus metric `sakin_alerts_created_total` rate > 10k/min
- High Kafka consumer lag on `normalized-events` topic
- Correlation service high CPU/memory usage
- Alert processing delays (check `sakin_correlation_latency_seconds`)
- User reports: "Alerts not appearing in dashboard"

## Diagnosis

### 1. Check Alert Metrics

```bash
# Query Prometheus
kubectl port-forward -n monitoring svc/prometheus 9090:9090

# Check alert creation rate
rate(sakin_alerts_created_total[1m])

# Check top rules triggering
topk(10, rate(sakin_alerts_created_total[5m])) by (rule_name)
```

### 2. Check Kafka Consumer Lag

```bash
# View consumer group lag
kafka-consumer-groups.sh --bootstrap-server kafka:9092 \
    --group sakin-correlation --describe

# Look for high lag on normalized-events topic
```

### 3. Check Service Health

```bash
# Check correlation service logs
kubectl logs -n sakin deployment/sakin-correlation --tail=100

# Check for error patterns
kubectl logs -n sakin deployment/sakin-correlation | grep -i "error\|exception"
```

## Resolution Steps

### Step 1: Identify Root Cause

**1a. Rule Triggering Excessively**

```bash
# Query top rules
kubectl exec -n sakin deploy/sakin-panel-api -- \
    psql -U postgres -d sakin_db \
    -c "SELECT rule_name, COUNT(*) as count 
        FROM alerts 
        WHERE created_at > NOW() - INTERVAL '5 minutes' 
        GROUP BY rule_name 
        ORDER BY count DESC 
        LIMIT 10;"
```

**Action**: Disable noisy rule temporarily

```bash
# Via API (requires admin token)
curl -X PATCH https://sakin-api/api/rules/{rule-id} \
    -H "Authorization: Bearer ${ADMIN_TOKEN}" \
    -d '{"enabled": false}'
```

**1b. Legitimate Attack/Event Surge**

Check event source distribution:

```bash
# Query ClickHouse for event sources
clickhouse-client --query "
    SELECT source_ip, COUNT(*) as count 
    FROM sakin.events 
    WHERE event_timestamp > now() - INTERVAL 5 MINUTE 
    GROUP BY source_ip 
    ORDER BY count DESC 
    LIMIT 20;"
```

**Action**: If attack confirmed, coordinate with security team for response

### Step 2: Reduce Load Temporarily

**2a. Increase Alert Deduplication Window**

```bash
# Update configuration
kubectl edit configmap -n sakin sakin-correlation-config

# Increase DedupTtlMinutes from 60 to 120
```

**2b. Scale Up Correlation Service**

```bash
# Horizontal scaling
kubectl scale deployment/sakin-correlation -n sakin --replicas=5

# Verify scaling
kubectl get pods -n sakin -l app=sakin-correlation
```

**2c. Temporarily Reduce Rule Sensitivity**

For critical rules only, consider temporarily increasing thresholds:

```bash
# Example: SSH brute force from 5 to 10 attempts
# Via database (emergency only)
kubectl exec -n sakin deploy/sakin-panel-api -- \
    psql -U postgres -d sakin_db \
    -c "UPDATE rules SET config = jsonb_set(config, '{threshold}', '10') 
        WHERE name = 'SSH Brute Force';"
```

### Step 3: Monitor Recovery

```bash
# Watch consumer lag decrease
watch -n 5 'kafka-consumer-groups.sh --bootstrap-server kafka:9092 \
    --group sakin-correlation --describe'

# Monitor alert rate
watch -n 5 'kubectl logs -n sakin deployment/sakin-correlation --tail=10 | grep "Alerts created"'
```

### Step 4: Post-Incident Actions

1. **Identify root cause rule/event**:
   - Query audit logs for recent rule changes
   - Review event patterns in ClickHouse

2. **Tune rule**:
   - Adjust threshold
   - Add exception conditions
   - Improve deduplication keys

3. **Document incident**:
   - Update incident log
   - Create postmortem if outage > 30min

## Prevention

### Long-term Solutions

1. **Implement rate limiting per rule**:
   ```csharp
   // In RuleEvaluator.cs
   if (alertsInLastMinute > maxAlertsPerMinute)
   {
       _logger.LogWarning("Rule {RuleName} throttled", rule.Name);
       return null;
   }
   ```

2. **Add alert aggregation**:
   - Group similar alerts by source/destination
   - Create summary alerts instead of individual ones

3. **Improve baseline detection**:
   - Better anomaly thresholds
   - Context-aware rules (time of day, day of week)

4. **Auto-scaling**:
   - Configure HPA (Horizontal Pod Autoscaler)
   ```yaml
   apiVersion: autoscaling/v2
   kind: HorizontalPodAutoscaler
   metadata:
     name: sakin-correlation-hpa
   spec:
     scaleTargetRef:
       apiVersion: apps/v1
       kind: Deployment
       name: sakin-correlation
     minReplicas: 2
     maxReplicas: 10
     metrics:
     - type: Resource
       resource:
         name: cpu
         target:
           type: Utilization
           averageUtilization: 70
   ```

## Rollback Plan

If scaling doesn't help:

1. **Pause correlation engine**:
   ```bash
   kubectl scale deployment/sakin-correlation -n sakin --replicas=0
   ```

2. **Events will queue in Kafka** (no data loss)

3. **Investigate offline**, fix rule/config

4. **Resume processing**:
   ```bash
   kubectl scale deployment/sakin-correlation -n sakin --replicas=3
   ```

## Related Runbooks

- [High Latency](./high-latency.md)
- [Memory Pressure](./memory-pressure.md)
- [Security Incident](./security-incident.md)

## Contacts

- **L1 On-Call**: Platform team
- **L2 Escalation**: SRE team
- **L3 Escalation**: Security team (if attack confirmed)
