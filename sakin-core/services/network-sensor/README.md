# Network Sensor

Network traffic monitoring service using SharpPcap and PacketDotNet.

## Overview

This service monitors network interfaces, captures packets, and extracts:
- HTTP URLs
- TLS SNI (Server Name Indication) hints
- Packet metadata

All findings are persisted to PostgreSQL.

## Components

- `Program.cs`: Bootstrap interface enumeration and database connectivity
- `PackageInspector.cs`: Per-device capture tasks and traffic monitoring
- `TLSParser.cs`: TLS SNI extraction support
- `Database.cs`: Database handler for recording packet metadata and SNI entries

## Requirements

- .NET 8.0
- PostgreSQL database
- Network interface access (may require elevated permissions)

## Usage

```sh
dotnet restore
dotnet build
dotnet run
```
