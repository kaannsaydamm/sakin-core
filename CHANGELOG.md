# S.A.K.I.N Platform Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Sprint 7: DevOps & Monitoring (v0.7.0) - 2024

#### Added

**Observability & Telemetry**
- Unified OpenTelemetry integration across all services
- Serilog JSON structured logging with console and OTLP exporters
- Prometheus metrics export from all microservices
- Distributed tracing support via Jaeger (OTLP gRPC)
- `/metrics` endpoint for all services (Prometheus scraping)
- Request logging middleware via Serilog ASP.NET Core integration

**Audit Logging Pipeline**
- New `IAuditLogger` interface for centralized audit event logging
- `KafkaAuditLogger` implementation for Kafka-based audit streaming
- Audit event schema with correlation IDs, user, action, and service tracking
- Integration with Alert Lifecycle Management (status changes)
- Integration with SOAR Playbook Execution (step and execution tracking)
- SOAR Worker audit events for alert action processing

**SOAR Service (Security Orchestration, Automation, Response)**
- New `sakin-soar` microservice for security automation
- Playbook execution engine with step orchestration
- Notification services: Slack, Email, Jira integration
- Agent command dispatcher for distributed task execution
- Audit logging for all playbook executions and steps
- Support for conditional execution and retry policies

**Analytics & ML Services**
- `Sakin.Analytics.ClickHouseSink`: Batch writer for normalized events to ClickHouse
- `Sakin.Analytics.BaselineWorker`: Hourly baseline calculation for anomaly detection
- Z-score based anomaly detection with configurable thresholds
- Statistical baseline tracking (mean, stddev, min, max) for behavioral analysis
- Redis-backed baseline caching with 25-hour TTL

**Monitoring Stack**
- Prometheus configuration with multi-service scraping
- Alertmanager setup with Slack integration
- Alert rules for service health, latency, and infrastructure metrics
- Grafana dashboards (4 templates):
  - Alerts Overview: rates, severity distribution, top rules
  - Playbook Execution: run counts, success rates, step metrics
  - Anomaly Detection: baseline vs actual, z-scores, anomalies
  - System Health: latency, lag, query times, agent status
- Jaeger distributed tracing (optional, toggle via compose)
- Health check endpoints (`/healthz`) for all services

**Docker Compose Enhancements**
- Added Prometheus, Alertmanager, Grafana, Jaeger services
- Added SOAR, ClickHouse Sink, Baseline Worker services
- Service healthchecks for all stateful services
- Proper dependency ordering and network configuration
- Volume management for persistent data

**Configuration & Secrets**
- Expanded `.env.example` with monitoring and SOAR variables
- Slack webhook, Jira credentials, SMTP server configuration
- OpenTelemetry endpoint configuration
- Prometheus scrape interval tuning options
- Grafana admin credentials management

**Service Program.cs Refactoring**
- Unified bootstrap pattern across all services using WebApplication builder
- Centralized Serilog configuration via `TelemetryExtensions`
- Automatic OpenTelemetry registration with `AddSakinTelemetry()`
- Prometheus endpoint mapping for all services
- Healthz endpoint for container health checks

**Kubernetes/Helm Scaffolding (Foundation)**
- Helm chart structure prepared in `deployments/k8s/helm/sakin-core/`
- Subchart templates for core services and dependencies
- Values files for different environments (dev, staging, prod)
- ServiceMonitor CRDs for Prometheus integration (planned)

**Documentation**
- Comprehensive monitoring guide (`deployments/monitoring/README.md`)
- Alert rules documentation with metrics reference
- Troubleshooting guide for monitoring stack
- Sprint 7 telemetry integration notes
- OpenTelemetry configuration examples

#### Changed

- All service Program.cs files now use modern WebApplication builder pattern
- Logging now standardized on Serilog with JSON output
- Audit service refactoring from simple Kafka publisher to structured pipeline
- SOAR integration refactored to use `IAuditLogger` instead of direct service
- Correlation and panel API services extended with telemetry support

#### Fixed

- Inconsistent logging across services (now unified Serilog)
- Missing structured field enrichment in logs (service, correlationId, userId, action)
- Unobservable microservice interactions (now traceable via Jaeger)

#### Deprecated

- Direct service `IAuditService.WriteAuditEventAsync()` - use `IAuditLogger.LogAuditEventAsync()` instead
- Console-only logging - use Serilog JSON structured logs

#### Security

- No changes, monitoring components run on internal network only
- Slack/Jira/SMTP credentials managed via environment variables
- Alertmanager webhook URLs configured securely via env

#### Technical Debt

- Helm chart templates need completion for all services
- Grafana dashboard JSON automation needed
- Performance tuning for high-volume metrics ingestion (future)

### Sprint 6: Risk Scoring & Threat Intelligence - 2024

#### Added

- Risk scoring engine with behavioral analysis
- GeoIP enrichment service integration
- Threat Intelligence async provider pattern (OTX, AbuseIPDB)
- User risk profile tracking with time-of-day patterns
- Asset caching for performance optimization

#### Changed

- Alert model extended with RiskScore, RiskLevel, and RiskFactors
- Event enrichment pipeline now includes GeoIP and threat intel lookups

### Sprint 5: Alert Lifecycle Management - 2024

#### Added

- Alert deduplication service with configurable windows
- Alert lifecycle state machine (New, Acknowledged, UnderInvestigation, Resolved, Closed, FalsePositive)
- Status history tracking with user and timestamp audit trail
- Alert repository persistence layer
- Panel API endpoints for alert lifecycle transitions

#### Changed

- Alert entity extended with lifecycle timestamps and status history

### Sprint 4 & Earlier

- Core event ingestion pipeline
- Correlation engine with rule-based alert generation
- Network sensor integration (Windows Event Log, Syslog, CEF)
- HTTP Collector for log ingestion
- HTTP API for panel UI and integrations
- PostgreSQL and Redis backends
- Kafka event streaming

---

## Release Notes

### Version 0.7.0-alpha (Sprint 7 Preview)

- 🚀 Full OpenTelemetry standardization across all services
- 📊 Production-grade monitoring stack with Prometheus, Grafana, Alertmanager
- 🔍 Distributed tracing via Jaeger for end-to-end observability
- 📝 Structured audit logging for compliance and forensics
- 🤖 SOAR service for security automation and playbook execution
- 📈 ML/Anomaly detection with statistical baselines
- 🐳 Enhanced Docker Compose with all supporting services
- 📚 Comprehensive documentation and troubleshooting guides

---

## Future Roadmap

- **Sprint 8**: Kubernetes production deployment with Helm charts
- **Sprint 9**: Advanced analytics with machine learning pipeline
- **Sprint 10**: Enterprise features (multi-tenancy, RBAC, SSO)
- **Longterm**: Horizontal scaling, auto-scaling, advanced threat intelligence
