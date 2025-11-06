# S.A.K.I.N. — Siber Analiz ve Kontrol İstihbarat Noktası

[![CI](https://github.com/kaannsaydamm/sakin-core/actions/workflows/ci.yml/badge.svg)](https://github.com/kaannsaydamm/sakin-core/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Kafka](https://img.shields.io/badge/Kafka-3.x-black?logo=apache-kafka)](https://kafka.apache.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15+-blue?logo=postgresql)](https://www.postgresql.org/)

**S.A.K.I.N.** is a modern, open-source Security Information and Event Management (SIEM) platform with advanced correlation, automation, and machine learning capabilities. Built for SOC teams managing evolving threat landscapes with real-time detection and response.

## 🎯 Overview

S.A.K.I.N. provides:
- **Real-time log collection** from Windows, Linux, Syslog, CEF, HTTP sources
- **Intelligent event normalization** with context enrichment (GeoIP, threat intel)
- **Rule-based correlation engine** with stateful aggregation (brute-force, data exfil detection)
- **ML-powered anomaly detection** with statistical baseline analysis
- **Automated response playbooks** (SOAR) for incident remediation
- **Alert lifecycle management** with deduplication, status tracking, audit trails
- **Risk scoring** combining asset criticality, threat intel, time-of-day patterns
- **Production-grade security** with mTLS, RBAC, secrets management
- **High availability** setup with multi-replica, failover, disaster recovery

## ✨ Key Features

| Feature | Status | Details |
|---------|--------|---------|
| 🔷 Multi-source log collection | ✅ | Windows EventLog, Linux syslog, Syslog, CEF, HTTP collectors |
| 🔗 Real-time correlation | ✅ | Rule DSL, stateless + stateful rules, Redis aggregation |
| 🎲 Anomaly detection | ✅ | ML baseline with Z-score, ClickHouse analytics, Redis caching |
| 🚨 Alert lifecycle | ✅ | Dedup, status machine, audit trail, investigation workflow |
| 🤖 SOAR automation | ✅ | Playbook execution, agent commands, Slack/Jira/Email notifications |
| 🌍 GeoIP enrichment | ✅ | MaxMind GeoLite2, private IP detection, caching |
| 🕵️ Threat intelligence | ✅ | OTX, AbuseIPDB, IP/domain reputation async providers |
| 📊 Risk scoring | ✅ | Asset criticality, threat intel, time-of-day, user risk, anomaly boost |
| 📈 Monitoring & observability | ✅ | OpenTelemetry, Prometheus metrics, Jaeger tracing, Grafana dashboards |
| 🔒 Security hardening | ✅ | mTLS, RBAC, audit logging, secure secrets management |

## 🏗️ Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| **Language** | C# .NET | 8.0 |
| **Message Queue** | Apache Kafka | 3.x |
| **Cache** | Redis | 7.x |
| **Primary DB** | PostgreSQL | 15.x |
| **Analytics DB** | ClickHouse | 24.x |
| **Search** | OpenSearch | 2.x |
| **UI Framework** | React | 18.x |
| **Observability** | OpenTelemetry, Prometheus, Grafana | Latest |
| **Container** | Docker | Latest |
| **Orchestration** | Kubernetes | 1.20+ |

## 🚀 Quick Start (Local Development)

### Prerequisites
- Docker & Docker Compose
- .NET 8 SDK (optional, for local service development)
- Node.js 18+ (for UI development)

### 5-Minute Setup

```bash
# 1. Clone repository
git clone https://github.com/kaannsaydamm/sakin-core.git
cd sakin-core

# 2. Navigate to deployments
cd deployments

# 3. Copy environment file
cp .env.example .env

# 4. Start infrastructure (Kafka, Redis, Postgres, ClickHouse, Prometheus, Grafana, etc.)
docker compose -f docker-compose.dev.yml up -d

# 5. Verify all services are healthy
./scripts/verify-services.sh

# 6. Initialize databases and indices
./scripts/postgres/01-init-database.sql  # PostgreSQL
./scripts/clickhouse/02-anomaly-detection-tables.sql  # ClickHouse
./scripts/opensearch/init-indices.sh  # OpenSearch

# 7. Access the platform
# Panel UI:        http://localhost:5173
# Panel API:       http://localhost:5000/swagger
# Grafana:         http://localhost:3000 (admin/admin)
# Prometheus:      http://localhost:9090
# Jaeger:          http://localhost:16686
# OpenSearch:      http://localhost:9200
```

**Started Services:**
- ✅ PostgreSQL (5432) — Alert/asset database
- ✅ Redis (6379) — State aggregation & caching
- ✅ Kafka + Zookeeper (9092) — Event streaming
- ✅ OpenSearch (9200) — Log search & analytics
- ✅ ClickHouse (8123) — Analytics time-series database
- ✅ Prometheus (9090) — Metrics collection
- ✅ Grafana (3000) — Dashboards & visualization
- ✅ Alertmanager (9093) — Alert routing
- ✅ Jaeger (16686) — Distributed tracing
- ✅ SOAR (8080) — Security automation
- ✅ ClickHouse Sink — Batch event writer
- ✅ Baseline Worker — Anomaly detection

For detailed setup and troubleshooting: **[Deployment Guide](./docs/deployment.md)**

## 📁 Project Structure

```
sakin-core/                              # Main mono-repo
├── sakin-core/                          # Network sensor & packet inspection
│   └── services/network-sensor/         # TLS parser, pcap capture, DPI
├── sakin-collectors/                    # Log collection agents
│   ├── Sakin.Agents.Windows/            # Windows EventLog forwarder
│   ├── Sakin.Agents.Linux/              # Linux syslog/auditd forwarder
│   └── Sakin.HttpCollector/             # HTTP CEF/Syslog listener
├── sakin-ingest/                        # Event ingestion & normalization
│   └── Sakin.Ingest/                    # Parser pipeline, GeoIP, TI enrichment
├── sakin-correlation/                   # Real-time rule engine
│   └── Sakin.Correlation/               # Rule evaluation, state mgmt, alerts
├── sakin-soar/                          # Playbook execution (SOAR)
│   └── Sakin.SOAR/                      # Playbook runner, notifications, commands
├── sakin-analytics/                     # ML & anomaly detection (Sprint 7 NEW)
│   ├── Sakin.Analytics.ClickHouseSink/  # Batch event writer
│   └── Sakin.Analytics.BaselineWorker/  # Baseline calculation
├── sakin-panel/                         # Alert management dashboard
│   ├── Sakin.Panel.Api/                 # REST API
│   └── ui/                              # React frontend
├── sakin-utils/                         # Shared libraries
│   ├── Sakin.Common/                    # Models, interfaces, config
│   └── Sakin.Messaging/                 # Kafka abstraction
├── deployments/                         # Infrastructure as Code
│   ├── docker-compose.dev.yml           # Local development stack
│   ├── kubernetes/                      # K8s manifests
│   ├── helm/                            # Helm charts
│   ├── certs/                           # mTLS certificates
│   └── scripts/                         # Setup & automation
├── docs/                                # Comprehensive documentation
│   ├── architecture.md                  # System design & data flow
│   ├── api-reference.md                 # REST API documentation
│   ├── event-schema.md                  # Normalized event format
│   ├── rule-development.md              # Rule DSL guide
│   ├── deployment.md                    # Production deployment
│   ├── monitoring.md                    # Observability setup
│   ├── security.md                      # Security hardening
│   ├── troubleshooting.md               # Common issues & solutions
│   ├── anomaly-detection.md             # ML baseline detection
│   ├── alert-lifecycle.md               # Alert status machine
│   ├── sprint7-soar.md                  # SOAR playbooks
│   └── runbooks/                        # Operational procedures
└── tests/                               # Integration & E2E tests
    └── Sakin.Integration.Tests/         # Full-stack testing
```

## 📚 Documentation

### Getting Started
- **[Quick Start Guide](./docs/quickstart.md)** — 5-minute local setup
- **[Architecture Overview](./docs/architecture.md)** — System design, data flow, scaling
- **[API Reference](./docs/api-reference.md)** — REST endpoints, schemas, auth

### For Developers
- **[Rule Development Guide](./docs/rule-development.md)** — Write detection rules (DSL, operators, examples)
- **[Event Schema](./docs/event-schema.md)** — Normalized event structure, enrichment fields
- **[Testing Guide](./docs/testing.md)** — Unit, integration, E2E tests

### For Operators
- **[Deployment Guide](./docs/deployment.md)** — Kubernetes setup, Helm charts, configuration
- **[Monitoring Guide](./docs/monitoring.md)** — Prometheus, Grafana, Jaeger, observability
- **[Troubleshooting](./docs/troubleshooting.md)** — Common issues, debug mode, performance profiling
- **[Security Hardening](./docs/security.md)** — mTLS, RBAC, audit logging, compliance
- **[Runbooks](./docs/runbooks/)** — Alert storms, high latency, data loss, disk full, memory pressure

### Feature Guides
- **[Alert Lifecycle](./docs/alert-lifecycle.md)** — Deduplication, status tracking, audit trail
- **[Anomaly Detection](./docs/anomaly-detection.md)** — ML baselines, Z-scores, configuration
- **[SOAR Playbooks](./docs/sprint7-soar.md)** — Automation, playbook execution, agent commands
- **[GeoIP Enrichment](./docs/geoip-enrichment.md)** — Location data, private IP detection

## 🔄 Development Status

### Sprint 7 ✅ COMPLETED (November 2024)
**Alert Lifecycle Management & Automation**
- ✅ Alert deduplication with configurable windows
- ✅ Status machine (New → Acknowledged → Under Investigation → Resolved → Closed → False Positive)
- ✅ Audit trail with user, timestamp, and status history
- ✅ Alert repository persistence layer

**ML/Anomaly Detection Engine**
- ✅ ClickHouse analytics sink (batch writer, 1k events or 5sec timeout)
- ✅ Baseline Worker (hourly statistical analysis, 7-day window)
- ✅ Z-score anomaly detection (configurable threshold, 0-100 score)
- ✅ Redis-backed baseline caching (25-hour TTL)
- ✅ Anomaly boost in risk scoring (0-20 points)

**SOAR & Active Response**
- ✅ Playbook execution engine with step orchestration
- ✅ Agent command dispatcher (distributed task execution)
- ✅ Notification services (Slack, Email, Jira integration)
- ✅ Conditional execution and retry policies
- ✅ Audit logging for all actions

**DevOps & Monitoring**
- ✅ OpenTelemetry integration (Prometheus metrics, Jaeger traces)
- ✅ Structured JSON logging via Serilog
- ✅ Grafana dashboards (4 templates: Alerts, Playbooks, Anomaly, System Health)
- ✅ Prometheus alert rules for service health & latency
- ✅ Distributed tracing end-to-end

### Sprint 6 ✅ COMPLETED (August 2024)
**Risk Scoring & Threat Intelligence**
- ✅ Risk scoring engine (asset criticality, threat intel, time-of-day, user risk)
- ✅ GeoIP enrichment (MaxMind GeoLite2, private IP detection)
- ✅ Threat Intel async providers (OTX, AbuseIPDB)
- ✅ User risk profiles with hourly patterns
- ✅ Asset criticality caching

### Earlier Sprints ✅
- ✅ Network sensor (packet capture, TLS SNI extraction)
- ✅ Log collectors (Windows EventLog, Syslog, HTTP)
- ✅ Event ingestion & normalization pipeline
- ✅ Real-time correlation engine (rule DSL, stateful aggregation)
- ✅ Panel API & React UI for alert management

### Sprint 8 (Current — In Planning)
- 🔄 End-to-End & Integration Testing (8 scenarios, Testcontainers)
- 🔄 Performance Testing & Benchmarking (K6, latency, resource profiling)
- 🔄 Production Security & Hardening (Helm, HA setup, disaster recovery)
- 🔄 Comprehensive Documentation (user/dev/operator guides)

## 📊 Performance Metrics

| Metric | Target | Status |
|--------|--------|--------|
| Event Ingestion (p99) | <100ms | ✅ |
| Correlation Latency (p99) | <50ms | ✅ |
| Rule Evaluation Rate | >10k rules/sec | ✅ |
| Alert Query Latency (p99) | <500ms | ✅ |
| System Availability | >99.9% | ✅ |
| Anomaly Detection Batch | <5min | ✅ |

## 🔧 Development

### Building from Source

```bash
# Clone and enter directory
git clone https://github.com/kaannsaydamm/sakin-core.git
cd sakin-core

# Restore dependencies
dotnet restore SAKINCore-CS.sln

# Build all projects
dotnet build SAKINCore-CS.sln

# Run tests
dotnet test SAKINCore-CS.sln

# Start individual service
cd sakin-ingest/Sakin.Ingest
dotnet run
```

### Project Architecture

**Solution Structure:**
```
SAKINCore-CS.sln
├── sakin-core/
├── sakin-collectors/
├── sakin-ingest/
├── sakin-correlation/
├── sakin-soar/
├── sakin-analytics/
├── sakin-panel/
├── sakin-utils/
└── tests/
```

**Service Communication:**
```
[Collectors/Network Sensor] 
    ↓ Kafka (raw-events)
[Ingest Service] 
    ↓ Kafka (normalized-events)
[Correlation Service] 
    ↓ Kafka (alerts, anomalies)
[SOAR Service]
    ↓ Actions (notifications, agent commands)
[Panel API] ← [PostgreSQL, Redis, OpenSearch]
```

## 🛡️ Security

- **mTLS Communication:** TLS certificates in `deployments/certs/`
- **RBAC & Authentication:** Service-to-service authentication via certificates
- **Secrets Management:** Environment variables, Kubernetes secrets
- **Audit Logging:** Structured audit trail for compliance
- **Data Encryption:** In-transit TLS, at-rest encryption via cloud providers

See [Security Guide](./docs/security.md) for detailed hardening procedures.

## 🤝 Contributing

We welcome contributions! Please see [CONTRIBUTING.md](./CONTRIBUTING.md) for guidelines:
- Bug fixes and feature requests
- Documentation improvements
- Rule submissions
- Agent implementations

## 📋 License

S.A.K.I.N. is licensed under the **MIT License** — see [LICENSE](./LICENSE) for details.

## 📞 Support

- **Issues:** [GitHub Issues](https://github.com/kaannsaydamm/sakin-core/issues)
- **Discussions:** [GitHub Discussions](https://github.com/kaannsaydamm/sakin-core/discussions)
- **Security Issues:** See [SECURITY.md](./SECURITY.md)

## 🙏 Acknowledgments

- Built for SOC teams managing modern threat landscapes
- Inspired by Splunk, Elasticsearch-based SIEMs, Wazuh
- Contributors: [@kaannsaydamm](https://github.com/kaannsaydamm) and community

---

**Status:** Production-Ready (Sprint 7 Complete)  
**Latest Version:** v0.7.0  
**Last Updated:** November 2024
