# SAKIN Platform Architecture

## Overview

SAKIN is a comprehensive security platform built using a mono-repo architecture. All platform components are organized in a single repository to facilitate code sharing, consistent versioning, and coordinated releases.

## Mono-Repo Structure

```
sakin-core/
├── README.md
└── services/
    └── network-sensor/          # Network traffic monitoring service
        ├── Handlers/
        │   └── Database.cs      # PostgreSQL database handler
        ├── Utils/
        │   ├── PackageInspector.cs  # Packet capture and analysis
        │   └── TLSParser.cs     # TLS SNI extraction
        ├── Program.cs           # Main entry point
        └── SAKINCore-CS.csproj

sakin-collectors/                # Data collection agents
sakin-ingest/                    # Data ingestion pipeline
sakin-msgbridge/                 # Message broker infrastructure
sakin-correlation/               # Event correlation engine
sakin-soar/                      # Security orchestration platform
sakin-panel/                     # Web UI and management console
sakin-utils/                     # Shared utilities
deployments/                     # Deployment configs and IaC
docs/                            # Platform documentation
```

## Component Details

### sakin-core

**Purpose**: Core services and foundational components

**Current Services**:
- **network-sensor**: Network traffic monitoring using SharpPcap and PacketDotNet
  - Monitors network interfaces
  - Extracts HTTP URLs and TLS SNI hints
  - Persists findings to PostgreSQL
  - Technologies: .NET 8, SharpPcap, PacketDotNet, Npgsql

**Future Services**:
- API gateway
- Authentication/authorization services
- Configuration management

### sakin-collectors

**Purpose**: Data collection agents for various data sources

**Planned Features**:
- Log file collectors
- Cloud service integrations (AWS, Azure, GCP)
- Security tool integrations (firewalls, IDS/IPS)
- Custom data source adapters

### sakin-ingest

**Purpose**: Data ingestion and normalization pipeline

**Planned Features**:
- Data parsing and transformation
- Schema normalization
- Data validation and enrichment
- Rate limiting and buffering

### sakin-msgbridge

**Purpose**: Message broker and event streaming

**Planned Features**:
- Message queue management
- Event routing and distribution
- Pub/sub messaging patterns
- Message persistence and replay

### sakin-correlation

**Purpose**: Event correlation and threat detection

**Planned Features**:
- Rule-based correlation engine
- Machine learning models for anomaly detection
- Threat intelligence integration
- Alert generation and prioritization

### sakin-soar

**Purpose**: Security Orchestration, Automation and Response

**Planned Features**:
- Workflow automation engine
- Playbook management
- Incident response automation
- Integration with external security tools

### sakin-panel

**Purpose**: Web-based user interface

**Planned Features**:
- Dashboard and visualization
- Alert management
- Configuration interface
- User management
- Currently hosted separately: https://github.com/kaannsaydamm/sakin-panel

### sakin-utils

**Purpose**: Shared code and utilities

**Planned Features**:
- Common data models
- Shared validation logic
- Utility functions
- Configuration management libraries

### deployments

**Purpose**: Deployment and infrastructure management

**Planned Contents**:
- Docker and Docker Compose files
- Kubernetes manifests and Helm charts
- CI/CD pipeline definitions
- Environment-specific configurations
- Infrastructure as Code (Terraform/Pulumi)

### docs

**Purpose**: Comprehensive platform documentation

**Planned Contents**:
- Architecture documentation
- API specifications (OpenAPI/Swagger)
- User guides and tutorials
- Development guidelines
- Deployment instructions
- Security best practices

## Data Flow

```
Network Traffic → network-sensor → PostgreSQL
                                 ↓
                          [Future Flow]
                                 ↓
                     sakin-collectors → sakin-ingest → sakin-msgbridge
                                                            ↓
                                                    sakin-correlation
                                                            ↓
                                                       sakin-soar
                                                            ↓
                                                       sakin-panel
```

## Technology Stack

### Current
- **Language**: C# (.NET 8)
- **Packet Capture**: SharpPcap 6.3.0
- **Packet Analysis**: PacketDotNet 1.4.7
- **Database**: PostgreSQL (via Npgsql 4.1.0)

### Planned
- **Message Broker**: RabbitMQ / Apache Kafka
- **API Framework**: ASP.NET Core
- **Frontend**: React/Next.js (sakin-panel)
- **Containerization**: Docker
- **Orchestration**: Kubernetes
- **CI/CD**: GitHub Actions / GitLab CI

## Development Guidelines

### Building the Solution

```bash
# Restore dependencies
dotnet restore

# Build all projects
dotnet build

# Run network sensor
cd sakin-core/services/network-sensor
dotnet run
```

### Adding New Services

1. Create a new directory under the appropriate top-level folder
2. Add README.md with service description
3. Create .csproj or appropriate project file
4. Update solution file to include new project
5. Document in this architecture file

### Naming Conventions

- **Directories**: kebab-case (e.g., `network-sensor`)
- **Services**: PascalCase for C# classes and namespaces
- **Files**: PascalCase for C# files (e.g., `Program.cs`)

## Migration Notes

The legacy SAKINCore-CS project has been migrated to `sakin-core/services/network-sensor` as part of the mono-repo restructuring. All functionality remains intact with updated project paths in the solution file.

## Future Roadmap

1. Implement sakin-ingest pipeline
2. Set up sakin-msgbridge with RabbitMQ
3. Develop sakin-correlation engine
4. Build sakin-soar automation platform
5. Integrate sakin-panel frontend
6. Create deployment configurations
7. Add comprehensive test coverage
8. Implement CI/CD pipelines
