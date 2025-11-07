# S.A.K.I.N. Security Hardening Checklist

This checklist ensures S.A.K.I.N. meets enterprise security standards for production deployment.

## Authentication & Authorization

### JWT Authentication
- [ ] JWT secret key is at least 32 characters (256 bits)
- [ ] JWT secret stored in secure secrets manager (not in config files)
- [ ] Token expiration set appropriately (≤60 minutes for access tokens)
- [ ] Token validation enabled (issuer, audience, lifetime, signature)
- [ ] Token blacklist implemented for revocation
- [ ] Refresh tokens stored securely with long expiration (7 days)

### API Key Authentication
- [ ] API keys generated with cryptographically secure random generator
- [ ] API keys hashed before storage (consider bcrypt/PBKDF2)
- [ ] API key rotation policy defined (≤6 months)
- [ ] Per-key rate limiting implemented
- [ ] API key audit logging enabled
- [ ] Unused/expired keys automatically disabled

### RBAC Implementation
- [ ] All roles defined with minimum required permissions
- [ ] Default deny policy (explicit permission required)
- [ ] Resource-level permissions enforced
- [ ] Permission checks performed at API layer
- [ ] Admin role restricted to specific users/services
- [ ] Agent role has no UI/data access

## Secrets Management

### Development
- [ ] `.env` files in `.gitignore`
- [ ] No secrets in `appsettings.json` (use User Secrets or environment variables)
- [ ] Secrets rotation tested in dev environment
- [ ] Documentation for local secrets setup

### Production
- [ ] All secrets in Kubernetes Secrets or Vault
- [ ] Secrets encrypted at rest (etcd encryption enabled)
- [ ] Secrets mounted as read-only files or environment variables
- [ ] No secrets in container images
- [ ] No secrets in logs
- [ ] No secrets in error messages
- [ ] Secrets rotation automated
- [ ] Secret access audited

### Critical Secrets
- [ ] Database passwords rotated annually (or on breach)
- [ ] Redis passwords set and rotated
- [ ] Kafka SASL credentials per service
- [ ] JWT signing key rotated on compromise
- [ ] API keys for external services (MaxMind, VirusTotal) secured
- [ ] TLS certificates managed with proper rotation

## mTLS Implementation

### Certificate Management
- [ ] CA certificate properly secured
- [ ] Service certificates signed by trusted CA
- [ ] Certificate expiration monitoring configured
- [ ] Auto-renewal 90 days before expiry
- [ ] Certificate revocation list (CRL) or OCSP configured
- [ ] Certificates stored in Kubernetes Secrets

### Service-to-Service mTLS
- [ ] All inter-service communication uses mTLS
- [ ] Certificate validation enforced
- [ ] Proper SAN (Subject Alternative Names) configured
- [ ] Certificate pinning for critical connections
- [ ] TLS 1.2+ enforced (no SSLv3, TLS 1.0, TLS 1.1)

### Kafka TLS/SASL
- [ ] Kafka brokers configured with TLS
- [ ] SASL mechanism enabled (SCRAM-SHA-256 recommended)
- [ ] Per-service Kafka credentials
- [ ] Kafka ACLs configured (principle of least privilege)
- [ ] Kafka audit logging enabled

## Input Validation & Sanitization

### Event Payload Validation
- [ ] Maximum event size enforced (64KB default)
- [ ] UTF-8 encoding validated
- [ ] Control characters filtered (except \n, \r, \t)
- [ ] Field length limits enforced
- [ ] Regex DoS prevention (timeout on pattern matching)
- [ ] Malformed events rejected early (parser-level)

### API Input Validation
- [ ] FluentValidation configured for all DTOs
- [ ] SQL injection prevented (parameterized queries)
- [ ] XSS prevention (output encoding)
- [ ] CSRF tokens for state-changing operations
- [ ] File upload validation (if applicable)
- [ ] Command injection prevented (no shell execution of user input)

### Parser-Specific Validation
- [ ] Syslog RFC compliance checked
- [ ] CEF field length validation
- [ ] JSON schema validation for structured logs
- [ ] Event timestamp validation (not in future, not too old)

## Audit Logging

### Coverage
- [ ] All authentication attempts logged (success + failure)
- [ ] All authorization failures logged
- [ ] All CRUD operations on critical resources logged
- [ ] All configuration changes logged
- [ ] All playbook executions logged
- [ ] All secret access logged
- [ ] All admin actions logged

### Log Storage
- [ ] Audit logs sent to Kafka (immutable)
- [ ] Audit logs stored in Postgres (queryable)
- [ ] Audit logs written to file (local backup)
- [ ] Audit logs retention: 2 years minimum
- [ ] Audit log integrity (consider signing/hashing)
- [ ] Audit log search API secured (admin-only)

### Log Content
- [ ] User identity captured
- [ ] Action/resource captured
- [ ] Timestamp (UTC) captured
- [ ] IP address captured
- [ ] User agent captured
- [ ] Old/new state captured (for updates)
- [ ] Request ID for correlation
- [ ] No PII in logs (or masked)

## GDPR & Compliance

### PII Handling
- [ ] PII identified in data model
- [ ] PII masked in logs (email → e***@d***.com)
- [ ] PII encrypted at rest (if stored)
- [ ] PII access controlled (RBAC)
- [ ] PII audit trail maintained

### User Rights
- [ ] Right to access: API to export user's data
- [ ] Right to delete: API to anonymize user's audit logs
- [ ] Right to rectification: API to update user data
- [ ] Right to restrict processing: opt-out mechanism
- [ ] Consent management: checkbox for ML training data usage

### Data Retention
- [ ] Retention policies defined per data type
- [ ] Automated cleanup jobs configured
- [ ] Data minimization principle applied
- [ ] Legal hold mechanism available

### Breach Notification
- [ ] Breach detection mechanisms in place
- [ ] Incident response plan documented
- [ ] DPA notification process defined (72 hours)
- [ ] User notification template prepared

## Network Security

### Network Policies
- [ ] Kubernetes NetworkPolicies applied
- [ ] Default deny ingress/egress
- [ ] Service-to-service traffic restricted
- [ ] External traffic allowlisted
- [ ] Database access limited to application pods

### Ingress/Egress
- [ ] TLS termination at ingress
- [ ] Rate limiting configured
- [ ] DDoS protection enabled (CloudFlare, AWS Shield)
- [ ] IP allowlisting for admin endpoints
- [ ] Egress filtering for data exfiltration prevention

### Service Mesh (Optional)
- [ ] Istio/Linkerd deployed for mTLS
- [ ] Automatic certificate rotation
- [ ] Circuit breaker configured
- [ ] Retry policies configured
- [ ] Observability enhanced

## High Availability

### Application Services
- [ ] Minimum 3 replicas for correlation engine
- [ ] Pod anti-affinity configured
- [ ] PodDisruptionBudget defined (minAvailable: 2)
- [ ] HorizontalPodAutoscaler configured
- [ ] Readiness/liveness probes configured
- [ ] Graceful shutdown implemented

### Databases
- [ ] PostgreSQL: primary + standby with streaming replication
- [ ] PostgreSQL: automated failover (Patroni or similar)
- [ ] PostgreSQL: connection pooling (PgBouncer)
- [ ] Redis: primary + replica + Sentinel
- [ ] Redis: persistence enabled (AOF)
- [ ] ClickHouse: replication configured
- [ ] Kafka: min 3 brokers, replication factor 3

### Load Balancing
- [ ] Load balancer distributes traffic evenly
- [ ] Health checks configured
- [ ] Session affinity disabled (stateless design)
- [ ] Connection draining on pod termination

## Backup & Disaster Recovery

### Backup Strategy
- [ ] PostgreSQL: daily backup + WAL archiving
- [ ] Redis: daily snapshot
- [ ] ClickHouse: partition-level exports (if needed)
- [ ] Kafka: no backup (data in Postgres/ClickHouse)
- [ ] Secrets: encrypted backup to S3/Azure Blob

### Backup Testing
- [ ] Restore tested monthly
- [ ] RTO/RPO documented and tested
- [ ] Restore automation scripts
- [ ] Backup integrity verification

### Disaster Recovery
- [ ] DR plan documented
- [ ] DR site configured (multi-region)
- [ ] Failover runbook tested
- [ ] Data replication to DR site
- [ ] DNS/load balancer failover automated

## Monitoring & Alerting

### Security Monitoring
- [ ] Failed authentication attempts alert
- [ ] Unusual API access patterns alert
- [ ] Privilege escalation alert
- [ ] Secret access alert
- [ ] Certificate expiration alert (30 days)
- [ ] Anomalous data export alert

### Operational Monitoring
- [ ] Service health checks
- [ ] Latency SLOs defined and monitored
- [ ] Error rate SLOs defined and monitored
- [ ] Resource utilization alerts
- [ ] Kafka consumer lag alerts
- [ ] Database connection pool exhaustion alerts

### Log Aggregation
- [ ] Centralized logging (ELK, Loki, CloudWatch)
- [ ] Log retention: 30 days
- [ ] Log search/filtering enabled
- [ ] Correlation ID in all logs
- [ ] Structured logging (JSON format)

## Container Security

### Image Security
- [ ] Base images from trusted sources
- [ ] Minimal base images (Alpine, Distroless)
- [ ] No secrets in images
- [ ] Image vulnerability scanning (Trivy, Snyk)
- [ ] Image signing (Notary, Cosign)
- [ ] Regular image updates

### Runtime Security
- [ ] Non-root user in containers
- [ ] Read-only root filesystem
- [ ] Security context constraints
- [ ] Resource limits defined
- [ ] Privileged containers prohibited
- [ ] Host network disabled

### Kubernetes Security
- [ ] RBAC configured (least privilege)
- [ ] Pod Security Standards enforced
- [ ] Admission controllers enabled
- [ ] Audit logging enabled
- [ ] Secrets encryption at rest
- [ ] API server secure

## Dependency Management

### Vulnerability Management
- [ ] Dependabot/Renovate configured
- [ ] Weekly dependency updates
- [ ] Security advisories monitored
- [ ] CVE patching within 7 days (critical)
- [ ] Vulnerability scanning in CI/CD

### Package Integrity
- [ ] NuGet package signing verification
- [ ] npm audit passing
- [ ] Lockfiles committed (package-lock.json, packages.lock.json)
- [ ] Private package registry (if needed)

## Incident Response

### Preparation
- [ ] Incident response plan documented
- [ ] On-call rotation defined
- [ ] Escalation paths documented
- [ ] Break-glass procedures documented
- [ ] Security team contacts documented

### Detection
- [ ] Security alerts configured
- [ ] Log monitoring active
- [ ] Anomaly detection enabled
- [ ] Threat intelligence integrated

### Response
- [ ] Runbooks for common incidents
- [ ] Evidence preservation procedures
- [ ] Communication templates
- [ ] Legal/PR contacts documented

### Recovery
- [ ] Backup restore procedures
- [ ] Secrets rotation procedures
- [ ] Post-incident review template

## Compliance Checklist

### Pre-Production
- [ ] Security review completed
- [ ] Penetration test conducted
- [ ] Vulnerability assessment completed
- [ ] Security sign-off obtained

### Production
- [ ] All checklist items completed
- [ ] Security monitoring active
- [ ] Incident response tested
- [ ] Compliance documentation ready

### Periodic Reviews
- [ ] Quarterly security reviews
- [ ] Annual penetration testing
- [ ] Regular compliance audits
- [ ] Security training for team

## Sign-off

- [ ] Security Team: _________________ Date: _______
- [ ] DevOps Lead: _________________ Date: _______
- [ ] CISO: ________________________ Date: _______
- [ ] Engineering Manager: __________ Date: _______

---

**Last Updated**: {{ date }}  
**Next Review**: {{ date + 90 days }}  
**Version**: 1.0
