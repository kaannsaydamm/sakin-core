# Disaster Recovery Plan — S.A.K.I.N.

## Overview

This document outlines the disaster recovery (DR) procedures for the S.A.K.I.N. Security Information and Event Management (SIEM) platform.

**Recovery Objectives**:
- **RTO (Recovery Time Objective)**: 1 hour
- **RPO (Recovery Point Objective)**: 15 minutes

## Disaster Scenarios

### 1. Complete Data Center Failure
- **Impact**: Total service outage
- **Recovery**: Failover to DR site
- **Estimated Time**: 30-60 minutes

### 2. Database Corruption/Loss
- **Impact**: Data loss, service degradation
- **Recovery**: Restore from backup
- **Estimated Time**: 30-45 minutes

### 3. Ransomware Attack
- **Impact**: Encrypted data, service outage
- **Recovery**: Restore from immutable backups
- **Estimated Time**: 2-4 hours

### 4. Kubernetes Cluster Failure
- **Impact**: Application unavailable
- **Recovery**: Rebuild cluster, restore state
- **Estimated Time**: 1-2 hours

### 5. Critical Service Component Failure
- **Impact**: Partial service degradation
- **Recovery**: Service restart or failover
- **Estimated Time**: 5-15 minutes

## Backup Strategy

### PostgreSQL Database

**Backup Schedule**:
- **Full Backup**: Daily at 2:00 AM UTC
- **WAL Archiving**: Continuous (every 16MB or 1 minute)
- **Retention**: 30 days local, 90 days S3

**Backup Location**:
- Primary: `/backups/postgres/` (NFS/PVC)
- Secondary: `s3://sakin-backups/postgres/`

**Backup Command**:
```bash
./deployments/scripts/backup/backup-postgres.sh
```

**Backup Verification**:
```bash
# List recent backups
ls -lh /backups/postgres/ | tail -10

# Verify backup integrity
pg_restore --list /backups/postgres/sakin_db_YYYYMMDD_HHMMSS.sql.gz

# Check S3 backups
aws s3 ls s3://sakin-backups/postgres/ --recursive | tail -10
```

### Redis

**Backup Schedule**:
- **RDB Snapshot**: Daily at 3:00 AM UTC
- **AOF**: Continuous (append-only file)
- **Retention**: 30 days

**Backup Location**:
- Primary: `/backups/redis/`
- Secondary: `s3://sakin-backups/redis/`

**Backup Command**:
```bash
./deployments/scripts/backup/backup-redis.sh
```

### ClickHouse

**Backup Strategy**:
- **No backup required** (30-day TTL, data can be regenerated from Kafka)
- **Optional**: Export partitions for long-term analytics

### Kafka

**Backup Strategy**:
- **No backup required** (data in PostgreSQL and ClickHouse)
- **Replication**: 3 replicas per partition

### Configuration & Secrets

**Backup Items**:
- Kubernetes manifests
- ConfigMaps
- Secrets (encrypted)
- Helm charts
- TLS certificates

**Backup Location**:
- Git repository (version controlled)
- `s3://sakin-backups/config/`

**Backup Command**:
```bash
# Export all Kubernetes resources
kubectl get all,configmap,secret -n sakin -o yaml > sakin-k8s-backup.yaml

# Encrypt and upload to S3
gpg --encrypt --recipient security@company.com sakin-k8s-backup.yaml
aws s3 cp sakin-k8s-backup.yaml.gpg s3://sakin-backups/config/$(date +%Y%m%d)/
```

## Recovery Procedures

### Scenario 1: PostgreSQL Database Failure

#### 1.1 Detect Failure

```bash
# Check if PostgreSQL is accessible
kubectl get pods -n sakin -l app=postgres
kubectl logs -n sakin postgres-0 --tail=50

# Verify database connectivity
psql -h postgres.sakin.svc.cluster.local -U postgres -d sakin_db -c "SELECT 1;"
```

#### 1.2 Determine Recovery Method

**Option A: Automatic Failover (if standby healthy)**
```bash
# Check standby status
kubectl exec -n sakin postgres-1 -- psql -U postgres -c "SELECT pg_is_in_recovery();"

# Promote standby to primary (Patroni handles automatically)
# Verify new primary
kubectl get svc -n sakin postgres-primary
```

**Option B: Restore from Backup**

1. **Stop all services** (prevent data inconsistency):
   ```bash
   kubectl scale deployment -n sakin --all --replicas=0
   ```

2. **Identify backup to restore**:
   ```bash
   # List available backups
   ls -lht /backups/postgres/ | head -10
   
   # Or from S3
   aws s3 ls s3://sakin-backups/postgres/ --recursive | tail -10
   ```

3. **Restore database**:
   ```bash
   ./deployments/scripts/backup/restore-postgres.sh \
       /backups/postgres/sakin_db_20250106_020000.sql.gz
   
   # Or from S3
   ./deployments/scripts/backup/restore-postgres.sh \
       s3://sakin-backups/postgres/20250106_020000/sakin_db_20250106_020000.sql.gz
   ```

4. **Verify restoration**:
   ```bash
   psql -h postgres.sakin.svc.cluster.local -U postgres -d sakin_db <<EOF
   \dt  -- List tables
   SELECT COUNT(*) FROM alerts;
   SELECT COUNT(*) FROM rules;
   SELECT MAX(created_at) FROM alerts;  -- Check last alert timestamp
   EOF
   ```

5. **Restore services**:
   ```bash
   kubectl scale deployment -n sakin --all --replicas=3
   ```

6. **Monitor recovery**:
   ```bash
   watch kubectl get pods -n sakin
   kubectl logs -n sakin deployment/sakin-correlation --tail=50 -f
   ```

#### 1.3 Verify Service Functionality

```bash
# Check API health
curl https://sakin-api.company.com/health

# Verify alert creation
curl -X POST https://sakin-api.company.com/api/test/event \
    -H "X-API-Key: your-api-key" \
    -d '{"type":"test","message":"DR test event"}'

# Check dashboard
open https://sakin.company.com
```

### Scenario 2: Complete Cluster Failure

#### 2.1 Provision New Cluster

```bash
# Create new Kubernetes cluster (adjust for your provider)
eksctl create cluster --name sakin-dr --region us-west-2 --nodes 5

# Or restore from IaC
terraform apply -var="cluster_name=sakin-dr"
```

#### 2.2 Restore Infrastructure Components

```bash
# 1. Install core infrastructure
helm install kafka bitnami/kafka -n kafka --create-namespace
helm install postgresql bitnami/postgresql -n sakin
helm install redis bitnami/redis -n sakin

# 2. Restore PostgreSQL from backup
kubectl exec -n sakin postgresql-0 -- /restore-script.sh

# 3. Apply S.A.K.I.N. manifests
kubectl apply -f deployments/kubernetes/namespace.yaml
kubectl apply -f deployments/kubernetes/secrets.yaml
kubectl apply -f deployments/kubernetes/configmaps.yaml
kubectl apply -f deployments/kubernetes/sakin-correlation-ha.yaml
kubectl apply -f deployments/kubernetes/sakin-ingest.yaml
kubectl apply -f deployments/kubernetes/sakin-panel-api.yaml
kubectl apply -f deployments/kubernetes/sakin-soar.yaml
```

#### 2.3 Verify and Failover

```bash
# Verify all pods running
kubectl get pods -n sakin

# Update DNS to point to new cluster
# (Manual step or automated via ExternalDNS)

# Verify traffic routing
curl https://sakin-api.company.com/health
```

### Scenario 3: Ransomware Attack

#### 3.1 Immediate Response

```bash
# 1. Isolate affected systems
kubectl delete networkpolicy -n sakin allow-external

# 2. Preserve evidence
kubectl logs -n sakin --all-containers --timestamps > /tmp/sakin-logs-$(date +%s).log

# 3. Identify infection vector
# Review audit logs, check for suspicious API calls

# 4. Do NOT pay ransom
```

#### 3.2 Recovery from Immutable Backups

```bash
# 1. Verify backups not encrypted
aws s3 ls s3://sakin-backups/postgres/ --recursive | tail -5

# 2. Provision clean cluster
# See Scenario 2

# 3. Restore from oldest known-good backup
./deployments/scripts/backup/restore-postgres.sh \
    s3://sakin-backups/postgres/20250105_020000/sakin_db_20250105_020000.sql.gz

# 4. Verify data integrity
# Compare checksums, query counts

# 5. Rotate ALL secrets
./deployments/scripts/rotate-secrets.sh --all
```

### Scenario 4: Service Component Failure

#### 4.1 Correlation Engine Failure

```bash
# Check pod status
kubectl get pods -n sakin -l app=sakin-correlation

# View logs
kubectl logs -n sakin deployment/sakin-correlation --tail=100

# Restart deployment
kubectl rollout restart deployment/sakin-correlation -n sakin

# If persistent issue, rollback
kubectl rollout undo deployment/sakin-correlation -n sakin
```

#### 4.2 Panel API Failure

```bash
# Similar to correlation engine
kubectl rollout restart deployment/sakin-panel-api -n sakin
```

## Failover Workflow

### Primary to DR Site Failover

```
┌─────────────────────────────────────────────────┐
│ 1. DETECT FAILURE                               │
│    - Monitoring alerts                          │
│    - Health checks fail 3+ times                │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│ 2. NOTIFY ON-CALL TEAM                          │
│    - PagerDuty/Opsgenie alert                   │
│    - Slack notification                         │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│ 3. ASSESS SITUATION                             │
│    - Check primary site status                  │
│    - Estimate recovery time                     │
│    - Decision: repair or failover?              │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│ 4. INITIATE FAILOVER (if needed)                │
│    - Promote DR database to primary             │
│    - Update DNS records                         │
│    - Scale up DR services                       │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│ 5. VERIFY FAILOVER                              │
│    - Health checks pass                         │
│    - Test event creation                        │
│    - Monitor dashboards                         │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│ 6. AUDIT & LOG                                  │
│    - Log failover event                         │
│    - Notify stakeholders                        │
│    - Create incident ticket                     │
└─────────────────────────────────────────────────┘
```

### DNS Failover

**Manual Failover**:
```bash
# Update Route53 (AWS example)
aws route53 change-resource-record-sets --hosted-zone-id Z1234567890ABC \
    --change-batch '{
      "Changes": [{
        "Action": "UPSERT",
        "ResourceRecordSet": {
          "Name": "sakin-api.company.com",
          "Type": "A",
          "TTL": 60,
          "ResourceRecords": [{"Value": "DR_SITE_IP"}]
        }
      }]
    }'
```

**Automated Failover** (Health Check Based):
```yaml
# Route53 health check with automatic failover
HealthCheck:
  Type: HTTPS
  ResourcePath: /health
  RequestInterval: 30
  FailureThreshold: 3
  
Failover:
  Primary: us-east-1 (primary site)
  Secondary: us-west-2 (DR site)
  HealthCheckId: health-check-123
```

## Testing & Validation

### Monthly DR Drills

**Schedule**: First Saturday of each month, 2:00 AM UTC

**Test Scenarios**:
1. Database restore from backup
2. Service restart and recovery
3. DNS failover simulation
4. Full cluster rebuild

**Test Procedure**:
```bash
# 1. Create test namespace
kubectl create namespace sakin-dr-test

# 2. Deploy test instance
kubectl apply -f deployments/kubernetes/ -n sakin-dr-test

# 3. Restore test database
./deployments/scripts/backup/restore-postgres.sh \
    /backups/postgres/sakin_db_latest.sql.gz \
    --target-namespace sakin-dr-test

# 4. Verify functionality
./scripts/dr-validation.sh --namespace sakin-dr-test

# 5. Cleanup
kubectl delete namespace sakin-dr-test
```

**Success Criteria**:
- [ ] Restore completes within RTO (1 hour)
- [ ] Data loss within RPO (15 minutes)
- [ ] All services healthy
- [ ] Alert processing functional
- [ ] API accessible
- [ ] Dashboard operational

### Backup Verification

**Weekly Backup Test**:
```bash
# Automated backup verification
./scripts/verify-backups.sh

# Checks:
# - Backup files exist and not corrupt
# - Backup size reasonable (not 0 bytes)
# - Can extract backup file
# - Backup metadata valid
# - S3 upload successful
```

## Communication Plan

### During Incident

**Internal Communication**:
- **Incident Channel**: #incident-sakin (Slack)
- **Updates**: Every 15 minutes
- **Roles**:
  - Incident Commander: Coordinates response
  - Tech Lead: Executes recovery
  - Communications Lead: Stakeholder updates

**External Communication**:
- **Status Page**: status.company.com/sakin
- **User Notification**: Email to admin contacts
- **Escalation**: CTO, CISO (for major incidents)

### Post-Incident

**Within 24 Hours**:
- Incident summary to stakeholders
- Root cause analysis initiated

**Within 7 Days**:
- Complete postmortem document
- Action items assigned
- Knowledge base updated

## Contacts

### On-Call Team
- **L1 (Platform)**: +1-XXX-XXX-XXXX, oncall-platform@company.com
- **L2 (SRE)**: +1-XXX-XXX-XXXX, oncall-sre@company.com
- **L3 (Engineering)**: +1-XXX-XXX-XXXX, oncall-engineering@company.com

### Management
- **CTO**: cto@company.com, +1-XXX-XXX-XXXX
- **CISO**: ciso@company.com, +1-XXX-XXX-XXXX
- **VP Engineering**: vp-eng@company.com

### External Vendors
- **AWS Support**: Enterprise support (24/7)
- **Database Consulting**: dba@company.com

## Appendix

### A. Backup Automation

Kubernetes CronJob for daily backups (already deployed):
```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: postgres-backup
spec:
  schedule: "0 2 * * *"  # Daily at 2 AM
  # See deployments/kubernetes/postgres-ha.yaml
```

### B. Recovery Time Estimates

| Scenario | RTO | RPO | Complexity |
|----------|-----|-----|------------|
| Service restart | 5 min | 0 | Low |
| Database failover | 15 min | 15 min | Medium |
| Database restore | 45 min | 15 min | Medium |
| Cluster rebuild | 2 hours | 15 min | High |
| Full DR failover | 1 hour | 15 min | High |

### C. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-01-06 | DevOps Team | Initial version |

---

**Next Review Date**: 2025-04-06  
**Document Owner**: SRE Team  
**Approval**: CTO, CISO
