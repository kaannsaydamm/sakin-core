# Sakin Core

## Overview
Core network monitoring and packet analysis services for the Sakin security platform.

## Components

### services/network-sensor
C# .NET 8 service that monitors network interfaces using SharpPcap and PacketDotNet. Captures packet metadata and extracts security-relevant information such as:
- HTTP URLs
- TLS SNI (Server Name Indication) data
- Source and destination IP addresses
- Protocol information

The network sensor uses modern dependency injection patterns and persists findings to PostgreSQL.

## Getting Started

```bash
cd services/network-sensor
dotnet restore
dotnet build
dotnet run
```

See [network-sensor documentation](./services/network-sensor/README.md) for detailed setup and configuration.

## Architecture
The core services use:
- .NET 8 Host Builder pattern
- Dependency Injection with Microsoft.Extensions.Hosting
- PostgreSQL for data persistence
- Configuration via appsettings.json with environment variable overrides
