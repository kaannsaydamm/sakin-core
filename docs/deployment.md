# Production Deployment Guide

## Overview

This guide covers deploying S.A.K.I.N. to production Kubernetes environments with high availability, disaster recovery, and security hardening.

## Prerequisites

- Kubernetes 1.20+ cluster (3+ master nodes recommended)
- kubectl configured with cluster access
- Helm 3.x installed
- 12+ CPU cores, 24GB+ RAM per node
- Persistent storage (AWS EBS, Azure Disk, etc)
- Container registry (Docker Hub, private registry, etc)

## Architecture

```
┌─────────────────────────────────────────────┐
│      Internet                               │
│      (Users, Data Sources)                  │
└────────────────┬────────────────────────────┘
                 │
    ┌────────────▼────────────┐
    │  Ingress Controller     │
    │  (TLS Termination)      │
    └────────────┬────────────┘
                 │
    ┌────────────▼────────────────────────┐
    │  Service Mesh (Istio/Linkerd)       │
    │  (Traffic Management, mTLS)         │
    └────────────┬─────────────────────────┘
                 │
    ┌────────────┴────────────┬────────────┐
    │                         │            │
    ▼                         ▼            ▼
  Services                Frontend         API
  (Correlation,           (React UI)       (REST)
   Ingest, SOAR, etc)
    │
    ├─────────────┬─────────────┬──────────┐
    │             │             │          │
    ▼             ▼             ▼          ▼
  Kafka        PostgreSQL    Redis      ClickHouse
  (Queues)     (Metadata)    (Cache)    (Analytics)
```

## Deployment Steps

### Step 1: Prepare Kubernetes Cluster

```bash
# Verify cluster connectivity
kubectl cluster-info
kubectl get nodes

# Create namespace
kubectl create namespace sakin
kubectl config set-context --current --namespace=sakin

# Create image pull secret (if using private registry)
kubectl create secret docker-registry regcred \
  --docker-server=registry.example.com \
  --docker-username=<username> \
  --docker-password=<password> \
  -n sakin
```

### Step 2: Install Prerequisites

#### Helm Repositories

```bash
# Add Helm repos
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo add jetstack https://charts.jetstack.io
helm repo add stable https://charts.helm.sh/stable
helm repo update
```

#### Storage Class

```bash
# Create persistent volume claims (varies by cloud provider)
# Example for AWS EBS:
kubectl create -f - <<EOF
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: sakin-storage
provisioner: ebs.csi.aws.com
parameters:
  type: gp3
  iops: "3000"
  throughput: "125"
EOF
```

#### Monitoring Stack

```bash
# Install Prometheus
helm install prometheus prometheus-community/kube-prometheus-stack \
  -n monitoring --create-namespace \
  --values monitoring-values.yaml

# Install Grafana
helm install grafana prometheus-community/grafana \
  -n monitoring
```

#### Service Mesh (Optional)

```bash
# Install Istio
istioctl install --set profile=production -y

# Enable sidecar injection
kubectl label namespace sakin istio-injection=enabled
```

### Step 3: Deploy Stateful Services

#### PostgreSQL

```bash
# Deploy PostgreSQL Operator
helm install postgresql bitnami/postgresql \
  --set auth.password=<strong-password> \
  --set primary.persistence.size=100Gi \
  --set replica.replicaCount=3 \
  -n sakin -f postgres-values.yaml

# Run migrations
kubectl run -it --rm postgres-client \
  --image=bitnami/postgresql:15 \
  -- psql -h postgresql -U postgres -f init-database.sql
```

**postgres-values.yaml:**
```yaml
primary:
  persistence:
    enabled: true
    size: 100Gi
    storageClassName: sakin-storage

replica:
  replicaCount: 3
  persistence:
    enabled: true
    size: 100Gi

metrics:
  enabled: true
  prometheus:
    enabled: true

backup:
  enabled: true
  cronjob:
    schedule: "0 2 * * *"
    storage:
      s3:
        bucket: sakin-backups
        region: us-east-1
```

#### Redis

```bash
helm install redis bitnami/redis \
  --set auth.password=<strong-password> \
  --set master.persistence.size=50Gi \
  --set replica.replicaCount=3 \
  -n sakin -f redis-values.yaml
```

**redis-values.yaml:**
```yaml
architecture: replication
auth:
  enabled: true

master:
  persistence:
    enabled: true
    size: 50Gi

replica:
  replicaCount: 3
  persistence:
    enabled: true

sentinel:
  enabled: true
  replicaCount: 3

metrics:
  enabled: true
```

#### Kafka

```bash
helm install kafka confluentinc/cp-helm-charts \
  --set brokers=3 \
  --set zookeeper.replicaCount=3 \
  --set topics.replicationFactor=3 \
  -n sakin -f kafka-values.yaml
```

#### ClickHouse

```bash
helm install clickhouse altinity/clickhouse \
  --set replicaCount=3 \
  --set persistence.size=500Gi \
  -n sakin -f clickhouse-values.yaml
```

### Step 4: Deploy S.A.K.I.N. Services

```bash
# Create secrets
kubectl create secret generic sakin-secrets \
  --from-literal=db-password=<password> \
  --from-literal=redis-password=<password> \
  --from-literal=slack-webhook=<webhook-url> \
  -n sakin

# Deploy services
helm install sakin ./deployments/helm/sakin-core \
  -n sakin \
  -f production-values.yaml
```

**production-values.yaml:**
```yaml
global:
  environment: production
  replicaCount: 3
  
  image:
    registry: registry.example.com
    pullPolicy: IfNotPresent
  
  secrets:
    externalSecrets: true
    name: sakin-secrets

correlation:
  replicas: 3
  resources:
    requests:
      memory: "2Gi"
      cpu: "1000m"
    limits:
      memory: "4Gi"
      cpu: "2000m"
  
  affinity:
    podAntiAffinity:
      preferredDuringSchedulingIgnoredDuringExecution:
      - weight: 100
        podAffinityTerm:
          labelSelector:
            matchExpressions:
            - key: app
              operator: In
              values:
              - sakin-correlation
          topologyKey: kubernetes.io/hostname

ingest:
  replicas: 2
  resources:
    requests:
      memory: "1Gi"
      cpu: "500m"
    limits:
      memory: "2Gi"
      cpu: "1000m"

panel:
  replicas: 2
  ingress:
    enabled: true
    className: nginx
    hosts:
    - host: sakin.example.com
      paths:
      - path: /
        pathType: Prefix
    tls:
    - secretName: sakin-tls
      hosts:
      - sakin.example.com
```

### Step 5: Configure High Availability

#### Pod Disruption Budgets

```yaml
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: sakin-correlation-pdb
spec:
  minAvailable: 2
  selector:
    matchLabels:
      app: sakin-correlation
```

#### Horizontal Pod Autoscaling

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
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

### Step 6: Network & Security

#### Network Policies

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: sakin-default-deny
spec:
  podSelector: {}
  policyTypes:
  - Ingress
  - Egress
---
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: sakin-ingress
spec:
  podSelector:
    matchLabels:
      app: sakin-panel
  policyTypes:
  - Ingress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
```

#### RBAC

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: sakin
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: sakin
rules:
- apiGroups: [""]
  resources: ["pods", "services"]
  verbs: ["get", "list", "watch"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: sakin
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: sakin
subjects:
- kind: ServiceAccount
  name: sakin
```

### Step 7: Backup & Disaster Recovery

#### Automated Backups

```bash
# Install Velero for backup
velero install --secret-file credentials-aws

# Create backup schedule
velero schedule create sakin-daily \
  --schedule "0 2 * * *" \
  -n sakin

# Verify backups
velero get backups
```

#### Restore Procedure

```bash
# List available backups
velero restore get

# Restore from backup
velero restore create --from-backup sakin-daily-20241106

# Monitor restore
velero restore describe sakin-daily-20241106
```

## Verification

### Health Checks

```bash
# Verify all pods running
kubectl get pods -n sakin

# Check service endpoints
kubectl get endpoints -n sakin

# View pod logs
kubectl logs -n sakin deployment/sakin-correlation

# Port forward to test services
kubectl port-forward -n sakin svc/sakin-panel 5000:5000
curl http://localhost:5000/healthz
```

### Load Testing

```bash
# Install k6
npm install -g k6

# Run load test
k6 run tests/load-test.js \
  --vus 100 \
  --duration 5m \
  --rps 1000
```

## Troubleshooting

### Pod Not Starting

```bash
# Check pod status
kubectl describe pod <pod-name> -n sakin

# View logs
kubectl logs <pod-name> -n sakin

# Check resource availability
kubectl top nodes
kubectl top pods -n sakin
```

### Service Connectivity Issues

```bash
# Test DNS resolution
kubectl run -it --rm busybox --image=busybox -- nslookup sakin-postgresql

# Test service connectivity
kubectl run -it --rm netcat --image=alpine:latest -- \
  nc -zv sakin-postgresql 5432

# Check network policies
kubectl get networkpolicies -n sakin
```

### Performance Issues

```bash
# Check resource usage
kubectl top pods -n sakin

# Monitor metrics
kubectl get --raw /apis/metrics.k8s.io/v1beta1/nodes

# Scale up if needed
kubectl scale deployment sakin-correlation --replicas=5
```

## Maintenance

### Regular Tasks

**Daily:**
- Verify backup completion
- Check service health
- Monitor error rates

**Weekly:**
- Review resource usage
- Update security patches
- Test disaster recovery

**Monthly:**
- Update dependencies
- Capacity planning review
- Security audit

### Upgrades

```bash
# Backup before upgrade
velero backup create pre-upgrade-backup

# Update Helm chart
helm repo update
helm upgrade sakin ./deployments/helm/sakin-core \
  -n sakin \
  -f production-values.yaml

# Verify upgrade
kubectl rollout status deployment/sakin-correlation -n sakin

# Rollback if needed
helm rollback sakin 1
```

## Cost Optimization

### Resource Optimization

```yaml
# Right-size requests/limits
resources:
  requests:
    memory: "512Mi"
    cpu: "250m"
  limits:
    memory: "1Gi"
    cpu: "500m"

# Use spot instances
nodeSelector:
  karpenter.sh/capacity-type: spot
```

### Data Management

```bash
# Archive old data to cold storage
kubectl exec -it postgresql -- \
  psql -c "DELETE FROM alerts WHERE created_at < now() - interval '90 days'"

# Clean old Kafka topics
kafka-topics --delete --topic old-events
```

## Next Steps

- Set up automated monitoring and alerting
- Configure backup retention policies
- Plan capacity for growth
- Implement change management procedures
- Document runbooks for operations team

---

**See Also:**
- [Quickstart Guide](./quickstart.md)
- [Architecture Overview](./architecture.md)
- [Monitoring Guide](./monitoring.md)
- [Troubleshooting](./troubleshooting.md)
