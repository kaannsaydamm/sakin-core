# Sakin Deployments

## Overview
Deployment configurations, scripts, and infrastructure-as-code for the Sakin security platform.

## Status
✅ **Active** - Docker Compose development environment is ready!

## Quick Start

### Using Automated Tools (Recommended)

The easiest way to set up your development environment:

```bash
# Navigate to tools directory
cd tools

# Complete setup (bootstrap + start + initialize)
make dev-setup

# Or step by step:
make bootstrap  # First time: setup directories and download GeoIP placeholders
make up         # Start infrastructure services
make init       # Initialize services (OpenSearch indices, etc.)
make test       # Run all tests
```

**Windows (PowerShell):**
```powershell
cd tools
.\dev-tools.ps1 dev-setup
```

See [tools/README.md](./tools/README.md) for all available commands.

### Manual Setup

```bash
# Start all infrastructure services
docker compose -f docker-compose.dev.yml up -d

# Verify all services are healthy
./scripts/verify-services.sh

# Initialize OpenSearch indices
./scripts/opensearch/init-indices.sh
```

See [DOCKER_SETUP.md](./DOCKER_SETUP.md) for detailed instructions.

## Purpose
This directory contains:
- ✅ Docker Compose configurations for development
- ✅ Database initialization scripts (PostgreSQL, ClickHouse)
- ✅ Search engine setup (OpenSearch)
- ✅ Service verification scripts
- ✅ Development automation tools (Makefile, PowerShell scripts)
- ✅ Bootstrap scripts for environment setup
- 🚧 Kubernetes manifests and Helm charts (planned)
- 🚧 Infrastructure-as-Code (Terraform, Pulumi, etc.) (planned)
- 🚧 CI/CD pipeline configurations (planned)

## Current Structure

```
deployments/
├── docker-compose.dev.yml       # ✅ Development environment composition
├── DOCKER_SETUP.md              # ✅ Comprehensive setup guide
├── README.md                    # This file
├── tools/                       # ✅ Development automation tools
│   ├── Makefile                 # ✅ Make targets for Linux/macOS
│   ├── dev-tools.ps1            # ✅ PowerShell script for Windows
│   ├── bootstrap.sh             # ✅ Bootstrap script (bash)
│   ├── bootstrap.ps1            # ✅ Bootstrap script (PowerShell)
│   └── README.md                # ✅ Tools documentation
└── scripts/
    ├── postgres/
    │   └── 01-init-database.sql # ✅ PostgreSQL schema initialization
    ├── clickhouse/
    │   └── 01-init-tables.sql   # ✅ ClickHouse tables and views
    ├── opensearch/
    │   └── init-indices.sh      # ✅ OpenSearch index templates
    └── verify-services.sh       # ✅ Service health verification
```

## Infrastructure Services

The Docker Compose environment provides:

| Service | Version | Ports | Purpose |
|---------|---------|-------|---------|
| PostgreSQL | 16-alpine | 5432 | Primary database for events and metadata |
| Redis | 7-alpine | 6379 | Caching and session storage |
| Zookeeper | 7.5.0 | 2181 | Kafka coordination |
| Kafka | 7.5.0 | 9092, 29092 | Event streaming and messaging |
| OpenSearch | 2.11.0 | 9200, 9600 | Search and log analytics |
| OpenSearch Dashboards | 2.11.0 | 5601 | Visualization UI |
| ClickHouse | 23.11-alpine | 8123, 9000 | OLAP analytics database |

## Planned Structure

### Docker
```
docker/
├── docker compose.yml          # Full stack composition
├── docker compose.dev.yml      # Development overrides
├── docker compose.prod.yml     # Production overrides
└── Dockerfiles/                # Service-specific Dockerfiles
```

### Kubernetes
```
kubernetes/
├── base/                       # Base manifests
├── overlays/                   # Environment-specific overlays
│   ├── dev/
│   ├── staging/
│   └── production/
└── helm/                       # Helm charts
```

### Infrastructure
```
terraform/                      # Terraform configurations
├── modules/                    # Reusable modules
├── environments/               # Environment-specific
│   ├── dev/
│   ├── staging/
│   └── production/
```

## Features

### Completed
- ✅ One-command local development setup (`make dev-setup`)
- ✅ Automated environment bootstrap (GeoIP setup, directory structure)
- ✅ Cross-platform support (Makefile for Linux/macOS, PowerShell for Windows)
- ✅ Service health verification
- ✅ Database initialization scripts
- ✅ Infrastructure services orchestration

### Planned
- 🚧 Production-ready Kubernetes deployments
- 🚧 Auto-scaling configurations
- 🚧 Secrets management integration
- 🚧 Monitoring and logging stack setup
- 🚧 Backup and disaster recovery scripts
- 🚧 Blue-green deployment support

## Technologies
- Docker & Docker Compose
- Kubernetes & Helm
- Terraform or Pulumi
- GitOps (ArgoCD/Flux)
