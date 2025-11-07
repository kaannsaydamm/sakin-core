# mTLS Certificates for S.A.K.I.N.

This directory contains scripts and certificates for mutual TLS (mTLS) authentication between S.A.K.I.N. services.

## Quick Start (Development)

```bash
# Generate all certificates
./generate-certs.sh

# Verify certificates
./verify-certs.sh
```

## Certificate Structure

```
ca.crt, ca.key              # Root CA (self-signed for dev)
├── sakin-ingest.crt/key    # Ingest service certificate
├── sakin-correlation.crt/key
├── sakin-panel-api.crt/key
├── sakin-soar.crt/key
├── sakin-enrichment.crt/key
└── sakin-collectors.crt/key
```

## Usage

### Local Development

1. Generate certificates:
   ```bash
   ./generate-certs.sh
   ```

2. Mount in Docker Compose:
   ```yaml
   volumes:
     - ./deployments/certs:/secrets/certs:ro
   ```

3. Enable TLS in service config:
   ```json
   {
     "TLS": {
       "Enabled": true,
       "CertificatePath": "/secrets/certs",
       "CertificateFileName": "sakin-ingest.crt",
       "PrivateKeyFileName": "sakin-ingest.key",
       "CaCertificateFileName": "ca.crt"
     }
   }
   ```

### Kubernetes Deployment

1. Apply Kubernetes secrets:
   ```bash
   kubectl apply -f k8s-secrets.yaml
   ```

2. Mount in pod:
   ```yaml
   volumeMounts:
     - name: tls-certs
       mountPath: /secrets/certs
       readOnly: true
   volumes:
     - name: tls-certs
       secret:
         secretName: sakin-ingest-tls
   ```

### Kafka TLS/SASL

For Kafka broker TLS:

```json
{
  "Kafka": {
    "BootstrapServers": "kafka:9093",
    "SecurityProtocol": "SaslSsl",
    "SaslMechanism": "SCRAM-SHA-256",
    "SaslUsername": "sakin-ingest",
    "SaslPassword": "your-password",
    "SslCaLocation": "/secrets/certs/ca.crt",
    "SslCertificateLocation": "/secrets/certs/sakin-ingest.crt",
    "SslKeyLocation": "/secrets/certs/sakin-ingest.key"
  }
}
```

## Production Considerations

⚠️ **These are development certificates only!**

For production:

1. **Use proper CA**: Let's Encrypt, internal PKI, or managed certificate service
2. **Certificate rotation**: Automate rotation before expiry
3. **Secrets management**: Use Vault, AWS Secrets Manager, or Azure Key Vault
4. **Never commit**: Add `*.key`, `*.p12`, `*.crt` to `.gitignore`

### Recommended Production Setup

#### Option 1: cert-manager (Kubernetes)

```yaml
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: sakin-ingest-tls
spec:
  secretName: sakin-ingest-tls
  issuerRef:
    name: sakin-ca-issuer
    kind: ClusterIssuer
  dnsNames:
    - sakin-ingest.sakin.svc.cluster.local
```

#### Option 2: HashiCorp Vault

```bash
# Enable PKI
vault secrets enable pki

# Generate root CA
vault write pki/root/generate/internal \
    common_name=sakin.internal \
    ttl=87600h

# Configure role
vault write pki/roles/sakin-service \
    allowed_domains=sakin.svc.cluster.local \
    allow_subdomains=true \
    max_ttl=720h
```

## Certificate Rotation

Certificates expire after 365 days (configurable in generate-certs.sh).

### Rotation Process

1. Generate new certificates 90 days before expiry
2. Deploy with both old and new certificates
3. Update services to use new certificates
4. Remove old certificates after verification
5. Audit rotation in logs

### Automated Rotation (Production)

Use cert-manager or external tools:
- **Kubernetes**: cert-manager
- **AWS**: ACM Private CA
- **Azure**: Key Vault
- **Vault**: PKI secrets engine

## Verification

Verify certificate validity:

```bash
# Check expiry
openssl x509 -in sakin-ingest.crt -noout -dates

# Verify against CA
openssl verify -CAfile ca.crt sakin-ingest.crt

# Check certificate chain
openssl s_client -connect sakin-ingest:8080 -CAfile ca.crt
```

## Troubleshooting

### Certificate validation failed

- Check clock synchronization (NTP)
- Verify CA certificate is trusted
- Check certificate expiry
- Verify SAN (Subject Alternative Names)

### Connection refused with TLS

- Ensure service is listening on TLS port
- Check firewall rules
- Verify certificate paths in config
- Check service logs for TLS errors

## Security Best Practices

1. **Least privilege**: Each service gets own certificate
2. **Short-lived**: Use short TTL (90 days or less)
3. **Rotation**: Automate rotation
4. **Monitoring**: Alert on expiry
5. **Revocation**: Implement CRL or OCSP
6. **Audit**: Log all certificate operations

## Related Documentation

- [Security Policy](../../SECURITY.md)
- [Deployment Guide](../../docs/deployment.md)
- [Secrets Management](../../docs/secrets-management.md)
