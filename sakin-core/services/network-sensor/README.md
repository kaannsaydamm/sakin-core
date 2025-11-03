# Network Sensor Service

A .NET 8 console application that monitors network interfaces and captures packet data including HTTP URLs and TLS SNI (Server Name Indication) information.

## Overview

This service uses SharpPcap and PacketDotNet to monitor network interfaces in promiscuous mode, extracting HTTP URLs and TLS SNI hints from captured packets. All findings are persisted to a PostgreSQL database.

## Architecture

The service follows modern .NET patterns:

- **Dependency Injection**: Uses `Microsoft.Extensions.DependencyInjection` for service registration
- **Configuration**: Leverages `appsettings.json` and environment variables for configuration
- **Host Builder Pattern**: Implements `IHostedService` for graceful startup/shutdown
- **Separation of Concerns**: Clear separation between handlers, utilities, and services

## Configuration

Configuration is managed through `appsettings.json`:

```json
{
  "Database": {
    "Host": "localhost",
    "Username": "postgres",
    "Password": "your_password",
    "Database": "network_db",
    "Port": 5432
  }
}
```

You can override configuration using environment variables:
- `Database__Host`
- `Database__Username`
- `Database__Password`
- `Database__Database`
- `Database__Port`

## Database Schema

The service expects two tables in PostgreSQL:

### PacketData
- `srcIp` (string): Source IP address
- `dstIp` (string): Destination IP address
- `protocol` (string): Protocol name
- `timestamp` (datetime): Capture timestamp

### SniData
- `sni` (string): Server Name Indication
- `srcIp` (string): Source IP address
- `dstIp` (string): Destination IP address
- `protocol` (string): Protocol name
- `timestamp` (datetime): Capture timestamp

## Running the Service

```bash
dotnet run
```

**Note**: This service requires elevated privileges to capture network packets. On Linux/macOS, you may need to run with `sudo`.

## Components

### Services
- **NetworkSensorService**: Background service that orchestrates the packet capture lifecycle

### Handlers
- **DatabaseHandler**: Manages PostgreSQL connections and data persistence
- **IDatabaseHandler**: Interface for database operations

### Utils
- **PackageInspector**: Captures and processes network packets
- **IPackageInspector**: Interface for packet inspection
- **TLSParser**: Parses TLS ClientHello messages to extract SNI

### Configuration
- **DatabaseOptions**: Strongly-typed configuration for database connection

## Migration Notes

This project is a migrated version of SAKINCore-CS with the following improvements:
- Modern .NET 8 Host Builder pattern
- Dependency injection throughout
- Configuration-based setup
- Better testability with interfaces
- Proper namespace alignment (Sakin.Core.Sensor)
