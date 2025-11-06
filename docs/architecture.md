# S.A.K.I.N. Architecture Overview

## System Architecture

S.A.K.I.N. is a modern, cloud-native SIEM platform built with a microservices architecture. Each component is independently deployable and scalable, communicating via Kafka event streaming.

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Data Sources                              │
├─────────────────────────────────────────────────────────────────┤
│  Network    │  Windows    │  Linux     │  Syslog   │  HTTP CEF  │
│  Sensor     │  EventLog   │  auditd    │  Devices  │  Devices   │
└──────┬──────┴──────┬──────┴───────┬────┴─────┬─────┴───────┬────┘
       │             │              │          │             │
       │             │              │          │             │
       └─────────────┴──────────────┴──────────┴─────────────┘
                            │
                            ▼
       ┌────────────────────────────────────────┐
       │     Kafka (raw-events topic)           │
       └────────────────────────────────────────┘
                            │
                            ▼
       ┌────────────────────────────────────────┐
       │   Ingest Service (Normalization)       │
       │  ├─ Parser Framework                   │
       │  ├─ GeoIP Enrichment                   │
       │  ├─ Threat Intelligence                │
       │  └─ Event Validation                   │
       └────────────────────────────────────────┘
                            │
                            ▼
       ┌────────────────────────────────────────┐
       │  Kafka (normalized-events topic)       │
       └────────────────────────────────────────┘
              │                       │
              ▼                       ▼
    ┌──────────────────┐  ┌────────────────────┐
    │  Correlation     │  │  ClickHouse Sink   │
    │  Engine          │  │  (Analytics)       │
    │ ├─ Stateless     │  │ ├─ Batch Writer    │
    │ ├─ Stateful      │  │ ├─ Event Storage   │
    │ ├─ Risk Scoring  │  │ └─ TTL Management  │
    │ └─ Anomaly       │  │                    │
    │   Detection      │  │ ┌────────────────┐ │
    │                  │  │ │ Baseline Worker│ │
    │ ┌──────────────┐ │  │ │ (Hourly Stats) │ │
    │ │ Redis State  │ │  │ └────────────────┘ │
    │ │ Aggregation  │ │  └────────────────────┘
    │ └──────────────┘ │
    └────────┬─────────┘
             │
             ▼
    ┌────────────────────┐
    │  Alert Dedup       │
    │  ├─ Time Windows   │
    │  ├─ Rule/Source    │
    │  │  Matching       │
    │  └─ Status Machine │
    └────────┬───────────┘
             │
             ▼
    ┌────────────────────────┐
    │  Kafka (alerts topic)  │
    └────────┬───────────────┘
             │
             ▼
    ┌────────────────────┐
    │  SOAR Service      │
    │  ├─ Playbook       │
    │  │  Execution      │
    │  ├─ Notifications  │
    │  │  (Slack/Email)  │
    │  └─ Agent Commands │
    └────────┬───────────┘
             │
             ▼
    ┌────────────────────────────────┐
    │  PostgreSQL Alert Database     │
    │  ├─ Alerts & Lifecycle         │
    │  ├─ Audit Trail                │
    │  └─ Investigation Data         │
    └────────┬───────────────────────┘
             │
             ▼
    ┌────────────────────────────────┐
    │  Panel UI & REST API           │
    │  ├─ Alert Management           │
    │  ├─ Playbook Management        │
    │  └─ Investigation Workflow     │
    └────────────────────────────────┘
```

## Component Responsibilities

### 1. Data Collection Layer

#### Network Sensor (sakin-core)
- **Purpose**: Deep packet inspection and network monitoring
- **Technology**: SharpPcap, PacketDotNet
- **Outputs**: HTTP URLs, TLS SNI, protocol metadata
- **Scaling**: One or more instances per network segment

#### Collectors (sakin-collectors)
- **Windows EventLog Agent**: Real-time Windows security events
- **Linux Auditd Agent**: Linux system audit events
- **HTTP CEF Collector**: Receives CEF-formatted logs
- **Syslog Listener**: RFC3164/RFC5424 syslog ingestion
- **Scaling**: Deployed as agents on endpoints

### 2. Ingestion Pipeline (sakin-ingest)

**Responsibilities:**
- Consume raw events from Kafka
- Parse events using format-specific parsers
- Normalize events to common schema
- Enrich with GeoIP data
- Query threat intelligence APIs
- Publish normalized events

**Key Features:**
- Multi-format parser framework (Windows, Linux, Syslog, CEF, Apache)
- Graceful error handling (passthrough normalization)
- GeoIP caching for performance
- Async threat intel lookups
- Configurable parser pipeline

### 3. Correlation Engine (sakin-correlation)

**Stateless Rules:**
- Single-event pattern matching
- Immediate alert generation
- Example: High-severity events, privilege escalations

**Stateful Rules:**
- Time-windowed aggregation via Redis
- Grouped by fields (IP, user, hostname)
- Examples: Brute-force (count failures), data exfil (volume threshold)

**Risk Scoring:**
- Asset criticality multiplier (1-5x)
- Threat intelligence reputation adjustment
- Time-of-day behavioral analysis
- User risk profile consideration
- Anomaly detection boost (0-20 points)

**Anomaly Detection:**
- Z-score calculation against baseline
- 0-100 normalized score
- Configurable threshold (default 2.5)
- Memory cache to prevent redundant calculations

### 4. Alert Lifecycle Management

**Deduplication:**
- Prevents duplicate alerts within time window
- Matches by rule + source fields
- Configurable window (default 300s)

**Status Machine:**
```
New → Acknowledged → Under Investigation → Resolved → Closed
                                        ↓
                                    False Positive
```

**Audit Trail:**
- User attribution for each status change
- Timestamp tracking
- Change reason/notes
- Full history queryable

### 5. SOAR Service (sakin-soar)

**Playbook Execution:**
- Event-driven by alerts
- Sequential step execution
- Conditional branching
- Retry policies for failed steps

**Notifications:**
- Slack integration (webhooks)
- Email via SMTP
- Jira ticket creation
- Custom webhooks

**Agent Commands:**
- Block IP on firewall
- Isolate/quarantine host
- Gather logs/forensics
- Execute custom scripts

### 6. Analytics Layer (sakin-analytics)

#### ClickHouse Sink
- Batch writer for normalized events
- 1000 events or 5-second timeout
- 30-day TTL for data retention
- Partitioned by month
- Bloom filters on username/hostname

#### Baseline Worker
- Hourly statistical analysis
- 7-day rolling window
- Calculates: mean, stddev, min, max
- Stores in Redis with 25-hour TTL
- Provides data for Z-score calculation

### 7. Panel (sakin-panel)

**REST API:**
- Alert queries with filtering
- Lifecycle status transitions
- Audit trail retrieval
- Playbook management

**UI Components:**
- Real-time alert dashboard
- Alert acknowledgment workflow
- Investigation timeline
- Severity distribution
- Rule performance metrics

## Data Flow

### Event Journey: Collection → Response

```
1. Collection
   └─ Network sensor captures packet
   └─ Agent forwards Windows EventLog
   └─ Syslog device sends message
   └─ HTTP CEF device posts event

2. Kafka Raw Events
   └─ Ingest consumer picks up event
   └─ Format-specific parser activated
   └─ Normalized to EventEnvelope
   └─ GeoIP enrichment applied
   └─ Threat Intel lookup (async)

3. Kafka Normalized Events
   ├─ Correlation engine rules applied
   │  ├─ Stateless rules: immediate alert
   │  ├─ Stateful rules: Redis aggregation
   │  ├─ Risk scoring calculated
   │  └─ Anomaly detection applied
   │
   └─ ClickHouse sink batch writes
      └─ Baseline worker reads 7-day window
      └─ Calculates statistical baselines

4. Alert Deduplication
   └─ Check Redis for recent duplicate
   └─ Window-based matching
   └─ If unique: persist to PostgreSQL

5. Kafka Alerts Topic
   └─ SOAR consumer triggered
   └─ Match playbook rules
   └─ Load playbook definition
   └─ Execute steps sequentially

6. SOAR Execution
   ├─ Notification step: Send Slack/Email/Jira
   ├─ Command step: Execute on agent
   ├─ Condition step: Branch logic
   └─ Audit each step result

7. Panel Access
   └─ Analyst queries alerts
   └─ Updates status (acknowledge, investigate)
   └─ Audit trail records change
   └─ Notifies team members
```

## Technology Stack

### Processing
- **.NET 8**: C# services, async/await patterns
- **OpenTelemetry**: Unified observability (metrics, traces, logs)
- **Serilog**: Structured JSON logging

### Data Streaming
- **Apache Kafka**: Event distribution, topic partitioning
- **Confluent Schema Registry**: Event schema versioning (optional)

### Storage
- **PostgreSQL**: Alerts, assets, playbooks, audit trail
- **Redis**: Aggregation state, baselines, caching
- **ClickHouse**: Time-series analytics, bulk queries
- **OpenSearch**: Log search and analysis

### Observability
- **Prometheus**: Metrics scraping and alerting
- **Grafana**: Dashboards and visualization
- **Jaeger**: Distributed tracing
- **Alertmanager**: Alert routing and aggregation

### Container & Orchestration
- **Docker**: Container runtime
- **Kubernetes**: Orchestration and scaling
- **Helm**: Package management
- **mTLS**: Service-to-service authentication

## Scaling Patterns

### Horizontal Scaling

**Kafka Consumer Groups:**
- Each service has consumer group (e.g., "correlation-service")
- Multiple instances share partition load
- Automatic rebalancing on instance addition/removal
- Max parallelism = number of partitions

**Scaling Example:**
```
# 1 partition: 1 instance
kafka-topics --create --topic normalized-events --partitions 1

# 10 partitions: can scale to 10 instances
kafka-topics --create --topic normalized-events --partitions 10
```

### Redis Optimization

- **Cluster Mode**: Multi-node for high availability
- **Replication**: Master-slave for persistence
- **Memory Management**: TTL-based key expiration
- **Pub/Sub**: For real-time notifications

### ClickHouse Scaling

- **Replication**: Multiple replicas for failover
- **Sharding**: Distributed queries across nodes
- **Partitioning**: Time-based partitions for query optimization
- **TTL**: Automatic deletion of old data

## Performance Characteristics

### Ingestion Latency (p99)
- Raw → Normalized: <50ms
- Normalized → Alert: <50ms
- Total pipeline: <100ms

### Throughput
- Target: 1000+ events/second
- Peak capacity: 5000+ EPS (depends on rule complexity)
- Bottleneck typically: Threat Intel API lookups

### Storage
- Event size: ~2KB average
- 1000 EPS × 86400 seconds = 86.4M events/day
- 30-day retention = ~2.6B events
- ClickHouse storage: ~5TB (compressed)

### Availability
- Target: 99.9% uptime
- RTO (Recovery Time Objective): <5 minutes
- RPO (Recovery Point Objective): <1 minute
- Multiple replicas per service
- Active-active deployment pattern

## Deployment Scenarios

### Development (Docker Compose)
- Single Docker Compose file
- All services in one network
- Shared volumes for data
- Minimal resources (2 CPU, 4GB RAM)

### Staging (Kubernetes)
- Single Kubernetes cluster
- 3+ master nodes
- Service mesh (optional)
- Persistent volumes for data

### Production (Kubernetes)
- Multi-zone deployment
- Auto-scaling enabled
- Backup automation
- Disaster recovery procedures
- Network policies enforced
- RBAC configured

## Security Architecture

### Network Security
- Service-to-service: mTLS required
- API endpoints: Token authentication
- Kafka: SASL/SSL configuration
- Database: Encrypted connections

### Secret Management
- Kubernetes Secrets for credentials
- Environment variable injection
- Vault integration (optional)
- No secrets in Git repository

### Audit & Compliance
- All actions logged to audit topic
- Immutable audit trail in PostgreSQL
- User attribution for changes
- Compliance query support

## Monitoring & Observability

### Metrics Collection
- Prometheus scrapes `/metrics` endpoint
- Custom metrics per service
- Request latency histograms
- Error rate tracking

### Alert Rules
- Service health alerts
- Latency threshold violations
- Error rate spikes
- Resource utilization alerts

### Distributed Tracing
- Jaeger receives traces from services
- Trace correlation IDs propagated
- End-to-end latency visualization
- Service dependency mapping

## Disaster Recovery

### Backup Strategy
- PostgreSQL: Daily full backup
- ClickHouse: Continuous replication
- Redis: AOF persistence
- Configuration: Version controlled

### Recovery Procedures
- RTO target: 5 minutes
- RPO target: 1 minute
- Test recovery monthly
- Maintain warm standby

## Capacity Planning

### Resource Requirements

**Per 1000 EPS:**
- 4 CPU cores
- 8GB RAM
- 10GB/day disk (ingestion)
- 500GB/month (30-day retention)

**Multi-region deployment (3 zones):**
- 12 CPU cores
- 24GB RAM
- Redundant infrastructure
- Geographic failover

## Future Architecture

### Planned Enhancements
- Advanced ML/AI models
- Graph database for relationships
- Multi-tenancy support
- Enhanced visualization

### Scalability Roadmap
- Serverless correlation engine
- Distributed rule evaluation
- ML model serving platform
- Enhanced cost optimization

---

See related documentation:
- [Deployment Guide](./deployment.md)
- [Event Schema](./event-schema.md)
- [Monitoring Guide](./monitoring.md)
- [Troubleshooting](./troubleshooting.md)
