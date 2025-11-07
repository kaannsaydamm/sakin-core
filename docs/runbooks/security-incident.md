# Runbook: Security Incident Response

## Overview

**Scenario**: Potential security breach, unauthorized access, or compromise detected  
**Impact**: CRITICAL - potential data breach, compliance violation  
**RTO**: Immediate response required  
**Escalation**: Immediate - notify security team, CTO, legal (if PII involved)

## Incident Types

1. **Unauthorized API Access**: Unknown client accessing API with stolen credentials
2. **Data Exfiltration**: Unusual data export patterns detected
3. **Service Compromise**: Evidence of malware, backdoor, or unauthorized code
4. **Credential Leak**: API keys, passwords, or tokens exposed
5. **DoS/DDoS Attack**: Service availability under attack

## Immediate Actions (DO NOT SKIP)

### 1. Contain the Incident

**DO THIS FIRST - Within 5 minutes**:

```bash
# 1a. Revoke compromised credentials
# If specific API key compromised:
kubectl exec -n sakin deploy/sakin-panel-api -- \
    psql -U postgres -d sakin_db \
    -c "UPDATE api_keys SET enabled = false WHERE key_id = '<compromised-key-id>';"

# 1b. Rotate JWT secret (forces all tokens invalid)
kubectl create secret generic sakin-jwt-secret \
    --from-literal=secret=$(openssl rand -base64 32) \
    --dry-run=client -o yaml | kubectl apply -f -

# Restart Panel API to pick up new secret
kubectl rollout restart deployment/sakin-panel-api -n sakin

# 1c. Block attacker IP at ingress
kubectl patch service sakin-ingress -n sakin -p \
    '{"spec":{"loadBalancerSourceRanges":["10.0.0.0/8","192.168.0.0/16"]}}'

# Or use network policy:
kubectl apply -f - <<EOF
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: block-attacker
  namespace: sakin
spec:
  podSelector:
    matchLabels:
      app: sakin-panel-api
  policyTypes:
  - Ingress
  ingress:
  - from:
    - ipBlock:
        cidr: 0.0.0.0/0
        except:
        - <attacker-ip>/32
EOF
```

### 2. Preserve Evidence

**CRITICAL - Do NOT delete logs yet**:

```bash
# 2a. Snapshot audit logs
kubectl exec -n sakin deploy/sakin-panel-api -- \
    psql -U postgres -d sakin_db \
    -c "\COPY (SELECT * FROM audit_events WHERE timestamp > NOW() - INTERVAL '24 hours') TO '/tmp/audit_evidence.csv' CSV HEADER"

# Extract from container
kubectl cp sakin/sakin-panel-api-xxx:/tmp/audit_evidence.csv ./evidence/audit_$(date +%s).csv

# 2b. Preserve application logs
kubectl logs -n sakin deployment/sakin-panel-api --all-containers --since=24h \
    > ./evidence/panel-api-logs-$(date +%s).log

kubectl logs -n sakin deployment/sakin-correlation --all-containers --since=24h \
    > ./evidence/correlation-logs-$(date +%s).log

# 2c. Export affected alerts
kubectl exec -n sakin deploy/sakin-panel-api -- \
    psql -U postgres -d sakin_db \
    -c "\COPY (SELECT * FROM alerts WHERE created_at > NOW() - INTERVAL '24 hours') TO '/tmp/alerts_evidence.csv' CSV HEADER"

# 2d. Snapshot Kafka audit topic
kafka-console-consumer.sh --bootstrap-server kafka:9092 \
    --topic audit-log --from-beginning --max-messages 10000 \
    > ./evidence/kafka-audit-$(date +%s).json

# 2e. Database snapshot (if data tampering suspected)
pg_dump -U postgres -d sakin_db -Fc -f ./evidence/db-snapshot-$(date +%s).dump
```

### 3. Notify Stakeholders

**Immediately notify**:

```bash
# Send alert to security team
curl -X POST https://slack.webhook.url \
    -H 'Content-Type: application/json' \
    -d '{
      "text": "🚨 SECURITY INCIDENT - S.A.K.I.N.",
      "attachments": [{
        "color": "danger",
        "fields": [
          {"title": "Severity", "value": "CRITICAL", "short": true},
          {"title": "Time", "value": "'"$(date)"'", "short": true},
          {"title": "Type", "value": "Unauthorized Access", "short": true},
          {"title": "Action", "value": "Credentials revoked, incident contained", "short": false}
        ]
      }]
    }'
```

## Investigation

### 4. Identify Scope

```bash
# 4a. Find all accesses by compromised credentials
kubectl exec -n sakin deploy/sakin-panel-api -- \
    psql -U postgres -d sakin_db <<EOF
SELECT 
    timestamp,
    user_name,
    action,
    resource_type,
    ip_address,
    status
FROM audit_events
WHERE user_name = '<compromised-user>'
  AND timestamp > NOW() - INTERVAL '7 days'
ORDER BY timestamp DESC;
EOF

# 4b. Check what data was accessed
kubectl exec -n sakin deploy/sakin-panel-api -- \
    psql -U postgres -d sakin_db <<EOF
SELECT 
    action,
    resource_type,
    COUNT(*) as count
FROM audit_events
WHERE user_name = '<compromised-user>'
GROUP BY action, resource_type
ORDER BY count DESC;
EOF

# 4c. Identify lateral movement
# Check if attacker accessed other services
grep -r "<attacker-ip>" /var/log/sakin/*/access.log

# 4d. Check for privilege escalation
kubectl exec -n sakin deploy/sakin-panel-api -- \
    psql -U postgres -d sakin_db <<EOF
SELECT * FROM audit_events
WHERE user_name = '<compromised-user>'
  AND action IN ('role_changed', 'permission_granted', 'api_key_created');
EOF
```

### 5. Assess Impact

```bash
# 5a. Check if PII/sensitive data accessed
kubectl exec -n sakin deploy/sakin-panel-api -- \
    psql -U postgres -d sakin_db <<EOF
SELECT COUNT(*) as pii_records_accessed
FROM audit_events
WHERE user_name = '<compromised-user>'
  AND resource_type IN ('user', 'asset', 'alert')
  AND action LIKE '%read%';
EOF

# 5b. Check if data modified/deleted
kubectl exec -n sakin deploy/sakin-panel-api -- \
    psql -U postgres -d sakin_db <<EOF
SELECT * FROM audit_events
WHERE user_name = '<compromised-user>'
  AND action IN ('update', 'delete', 'bulk_delete')
ORDER BY timestamp DESC;
EOF

# 5c. Check if rules/configs changed
kubectl exec -n sakin deploy/sakin-panel-api -- \
    psql -U postgres -d sakin_db <<EOF
SELECT 
    timestamp,
    action,
    resource_id,
    old_state,
    new_state
FROM audit_events
WHERE user_name = '<compromised-user>'
  AND resource_type IN ('rule', 'config', 'playbook');
EOF
```

## Recovery

### 6. Restore Integrity

```bash
# 6a. Restore modified rules (if tampered)
kubectl exec -n sakin deploy/sakin-panel-api -- \
    psql -U postgres -d sakin_db <<EOF
-- Restore from audit old_state
UPDATE rules 
SET config = (SELECT old_state FROM audit_events WHERE resource_id = rules.id LIMIT 1)
WHERE id IN (SELECT resource_id FROM audit_events WHERE user_name = '<compromised-user>' AND resource_type = 'rule');
EOF

# 6b. Revert configuration changes
kubectl rollout undo deployment/sakin-panel-api -n sakin

# 6c. Restore database from backup (if data corrupted)
# See backup-restore runbook

# 6d. Rotate ALL secrets (not just compromised)
./deployments/scripts/rotate-secrets.sh --all
```

### 7. Strengthen Security

```bash
# 7a. Enable MFA (if not already)
# Update auth configuration

# 7b. Implement IP allowlisting
kubectl apply -f - <<EOF
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: sakin-allowlist
  namespace: sakin
spec:
  podSelector: {}
  policyTypes:
  - Ingress
  ingress:
  - from:
    - ipBlock:
        cidr: 10.0.0.0/8  # Internal network only
    - ipBlock:
        cidr: 203.0.113.0/24  # Office IPs
EOF

# 7c. Reduce token TTL
# Update appsettings.json
"Jwt": {
  "AccessTokenExpirationMinutes": 15  // Reduced from 60
}

# 7d. Enable enhanced audit logging
"AuditLogging": {
  "Enabled": true,
  "IncludePayload": true,  // Log full request payloads
  "LogFailedAuthAttempts": true
}
```

## Post-Incident

### 8. Document & Report

Create incident report with:

1. **Timeline**:
   - When breach detected
   - When contained
   - When resolved

2. **Root Cause**:
   - How attacker gained access
   - What was compromised

3. **Impact Assessment**:
   - Data accessed/modified
   - Systems affected
   - Business impact

4. **Response Actions**:
   - Containment steps
   - Evidence preserved
   - Recovery actions

5. **Lessons Learned**:
   - What worked well
   - What needs improvement
   - Action items

### 9. Compliance Notifications

If PII involved:

- **GDPR**: Notify DPA within 72 hours
- **HIPAA**: Notify HHS if PHI breached
- **Other**: Check applicable regulations

### 10. Action Items

- [ ] Update security policies
- [ ] Implement additional controls
- [ ] Conduct security training
- [ ] Schedule penetration test
- [ ] Review access controls
- [ ] Implement SIEM alerting improvements

## Prevention Checklist

- [ ] All secrets stored in Vault/Secrets Manager
- [ ] API authentication enforced (no anonymous access)
- [ ] RBAC properly configured
- [ ] Audit logging enabled and monitored
- [ ] Network policies restrict traffic
- [ ] Regular security scanning (Trivy, Snyk)
- [ ] Dependency updates automated (Dependabot)
- [ ] Incident response plan tested quarterly
- [ ] Team trained on incident response
- [ ] Break-glass procedures documented

## Related Documentation

- [Security Policy](../../SECURITY.md)
- [Audit Logging](../../docs/monitoring.md#audit-logging)
- [Disaster Recovery](./data-loss.md)

## Contacts

- **Security Team**: security@company.com (24/7)
- **L3 Escalation**: CTO, CISO
- **Legal**: legal@company.com (if PII involved)
- **PR/Communications**: pr@company.com (if public disclosure needed)

## Emergency Contacts

- **On-Call Security**: +1-XXX-XXX-XXXX
- **Incident Commander**: [Name, Phone]
- **Legal Counsel**: +1-XXX-XXX-XXXX
