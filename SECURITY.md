# Security Policy

## Reporting Security Issues

**IMPORTANT:** Please do not create public GitHub issues for security vulnerabilities. Instead, report security issues privately to ensure they can be addressed before public disclosure.

### Reporting Process

1. **Email the maintainers:**
   - Send details to: [security contact to be added]
   - Subject line: `[SECURITY] S.A.K.I.N. Vulnerability Report`

2. **Include:**
   - Description of the vulnerability
   - Affected components/versions
   - Steps to reproduce (if possible)
   - Potential impact assessment
   - Suggested fix (if available)

3. **Timeline:**
   - We will acknowledge receipt within 24 hours
   - Initial assessment within 3-5 business days
   - Updates on progress every 3-5 days
   - Target fix time: 30 days from initial report
   - Coordinated disclosure before public release

## Security Practices

### Development

- **Code Review**: All changes go through peer review
- **Dependency Management**: Regular scanning for vulnerable dependencies
- **Static Analysis**: Code scanning with Roslyn analyzers
- **Testing**: Comprehensive unit and integration test coverage
- **Secrets Management**: No hardcoded secrets in repository

### Dependencies

We monitor and update dependencies regularly:

```bash
# Check for vulnerable packages
dotnet list package --vulnerable

# Update packages
dotnet package update
```

### Authentication & Authorization

- **Service-to-Service**: mTLS with certificate-based authentication
- **API Authentication**: Token-based authentication for API endpoints
- **RBAC**: Role-based access control for user actions
- **Secrets**: Kubernetes secrets or environment variables for sensitive data

### Encryption

- **In-Transit**: TLS 1.2+ for all network communication
- **At-Rest**: Database encryption (AWS RDS, Azure SQL, etc.)
- **Credentials**: Encrypted secrets management via platform providers

### Audit Logging

- All administrative actions are logged
- Audit logs include timestamp, user, action, and result
- Audit events are immutable and retained per compliance policy
- Structured JSON logging for queryability

## Security Configuration

### Minimal Deployment

```yaml
# kubernetes/overlays/security/kustomization.yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

commonLabels:
  app: sakin

replicas:
  - name: sakin-correlation
    count: 3

patchesStrategicMerge:
  - pod-security-policy.yaml
  - network-policy.yaml
  - rbac.yaml
```

### Network Security

- Services run in isolated namespaces
- Network policies restrict inter-service communication
- Ingress controller handles external traffic
- Service mesh (Istio/Linkerd) for advanced traffic control

### Pod Security

- Non-root container users
- Read-only root filesystems where possible
- No privileged containers except network-sensor
- Security context constraints enforced

### Secrets Management

**Kubernetes:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: sakin-secrets
type: Opaque
data:
  db-password: <base64-encoded>
  slack-webhook: <base64-encoded>
```

**Environment Variables:**
```bash
# Never commit secrets
DB_PASSWORD=$(kubectl get secret sakin-secrets -o jsonpath='{.data.db-password}' | base64 -d)
SLACK_WEBHOOK=$(kubectl get secret sakin-secrets -o jsonpath='{.data.slack-webhook}' | base64 -d)
```

## Data Protection

### GDPR Compliance

- Personal data is collected only when necessary
- Users can request data deletionhttps://github.com/kaannsaydamm/sakin-core/tree/main
- Data retention policies are enforced
- Data processing agreements are in place

### Data Retention
https://github.com/kaannsaydamm/sakin-core/tree/main
- Events: Stored for 30 days (configurable)
- Alerts: Stored for 90 days (configurable)
- Audit logs: Stored for 1 year (configurable)
- User data: Retained while account is active

### Data Minimization

- Only necessary data is collected
- PII is anonymized where possible
- Sensitive fields are excluded from logging
- Event sampling for high-volume scenarios

## Access Control

### Role-Based Access Control (RBAC)

**Predefined Roles:**
- **Admin**: Full access to all functions
- **Analyst**: View alerts, create playbooks, manage investigations
- **Operator**: View-only access to dashboards
- **Viewer**: Read-only access to alerts

**Example Kubernetes RBAC:**
```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: sakin-analyst
rules:
- apiGroups: [""]
  resources: ["alerts"]
  verbs: ["get", "list", "watch", "update"]
- apiGroups: [""]
  resources: ["playbooks"]
  verbs: ["get", "list", "create", "update"]
```

### API Authentication

All API endpoints require authentication tokens:

```bash
# Get authentication token
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "user", "password": "pass"}'

# Use token in requests
curl -H "Authorization: Bearer <token>" \
  http://localhost:5000/api/alerts
```

## Vulnerability Disclosure

### Supported Versions

| Version | Status | Support Until |
|---------|--------|---------------|
| 0.7.x   | Supported | Current |
| 0.6.x   | Deprecated | 2025-06-01 |
| <0.6.0  | EOL | Unsupported |

### CVE Handling

When a CVE is discovered:

1. **Assessment**: Impact on S.A.K.I.N. is evaluated
2. **Fix**: Patch is developed and tested
3. **Release**: Security patch is released (if needed)
4. **Advisory**: Security advisory is published
5. **Notification**: Users are notified of available updates

## Security Recommendations

### For Operators

1. **Keep Systems Updated**
   - Regularly update S.A.K.I.N. services
   - Keep dependencies current
   - Apply OS and container security patches

2. **Network Security**
   - Use network policies to isolate services
   - Implement service mesh for traffic control
   - Use firewalls to restrict external access
   - Monitor network traffic

3. **Access Control**
   - Enforce strong password policies
   - Use multi-factor authentication
   - Regularly audit user access
   - Implement principle of least privilege

4. **Monitoring & Alerting**
   - Monitor for suspicious access patterns
   - Alert on authentication failures
   - Track privilege escalations
   - Monitor configuration changes

5. **Data Protection**
   - Encrypt databases at rest
   - Use encrypted backups
   - Implement disaster recovery procedures
   - Test backup restoration regularly

6. **Compliance**
   - Audit logs for regulatory requirements
   - Maintain change management process
   - Document security procedures
   - Conduct regular security reviews

### For Developers

1. **Secure Coding**
   - Validate all inputs
   - Use parameterized queries to prevent SQL injection
   - Escape output to prevent XSS
   - Use crypto libraries correctly

2. **Dependency Management**
   - Review dependency licenses
   - Pin dependency versions
   - Use tools to detect vulnerable dependencies
   - Keep dependencies updated

3. **Testing**
   - Include security tests in test suite
   - Test error handling and edge cases
   - Test with invalid/malicious inputs
   - Perform manual security review

4. **Code Review**
   - Review security implications of changes
   - Check for hardcoded secrets
   - Verify authentication/authorization
   - Check for common vulnerabilities

## Security Tools

### Development

- **Roslyn Analyzers**: C# code analysis for security issues
- **OWASP ZAP**: API security testing
- **Dependabot**: Automated dependency vulnerability scanning
- **Sonarqube**: Code quality and security analysis

### Operations

- **Falco**: Runtime security monitoring
- **Trivy**: Container image vulnerability scanning
- **Kube-bench**: Kubernetes security assessment
- **kubesec**: Kubernetes YAML security scoring

## Security Checklist

### Pre-Deployment

- [ ] All dependencies are up to date
- [ ] Security scanning passed with no high/critical issues
- [ ] Secrets are not hardcoded or in version control
- [ ] TLS is enabled for all network communication
- [ ] Authentication/authorization is properly configured
- [ ] Audit logging is enabled
- [ ] Network policies are in place
- [ ] RBAC is properly configured

### Post-Deployment

- [ ] Monitor for security alerts
- [ ] Review audit logs regularly
- [ ] Update systems regularly
- [ ] Conduct security reviews monthly
- [ ] Test disaster recovery procedures
- [ ] Review user access quarterly
- [ ] Perform penetration testing annually

## Contact

For security issues and questions:
- **Private Security Report**: [kaannsaydamm@proton.me]
- **Security Advisory**: [GitHub Security Advisory]
- **Responsible Disclosure**: Follow 90-day disclosure timeline

---

Last Updated: November 2025
