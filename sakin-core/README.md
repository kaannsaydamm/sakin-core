# Sakin Core — Network Monitoring & Packet Inspection

## Overview

The core module provides network packet capture and analysis capabilities for the S.A.K.I.N. SIEM platform. It performs deep packet inspection (DPI), extracts security-relevant information from network traffic, and feeds normalized events into the correlation engine for threat detection.

## Key Features

### Packet Capture
- **Real-time Capture**: Continuous monitoring of network interfaces
- **Pcap Integration**: SharpPcap library for cross-platform capture
- **Protocol Support**: IPv4, IPv6, TCP, UDP, DNS, HTTP, TLS/SSL
- **Performance**: Zero-copy packet filtering with BPF (Berkeley Packet Filter)

### Protocol Parsing
- **HTTP Analysis**: URL extraction, host headers, request/response inspection
- **TLS/SSL Analysis**: SNI (Server Name Indication) extraction, certificate details
- **DNS Analysis**: Query/response parsing, domain resolution tracking
- **Network Layer**: IP addresses, ports, protocols, packet payloads

### Data Extraction
- **Security Events**: Authentication attempts, suspicious traffic patterns
- **Application Layer**: URLs, domains, hostnames from traffic
- **Metadata**: Timestamps, packet sizes, protocol versions
- **Behavioral Data**: Connection patterns, data volume analysis

## Architecture

```
Network Interface
      │
      ▼
[SharpPcap Capture]
      │
      ├─▶ Packet Filtering (BPF)
      │
      ├─▶ [Protocol Parsers]
      │   ├─ HTTP Parser ──▶ URLs, Hosts
      │   ├─ TLS Parser  ──▶ SNI, Certificates
      │   └─ DNS Parser  ──▶ Domains, Queries
      │
      ├─▶ [Event Creation]
      │   └─ NetworkEvent (IP, Port, Protocol, Payload)
      │
      ├─▶ [Normalization]
      │   └─ EventEnvelope (source, type, timestamp)
      │
      ├─▶ Kafka Producer
      │
      ▼
[Kafka (raw-events topic)]
      │
      ▼
[Ingest Service]
```

## Components

### services/network-sensor

C# .NET 8 service that monitors network interfaces using SharpPcap and PacketDotNet. Captures:
- HTTP URLs and headers
- TLS SNI (Server Name Indication) data
- Source and destination IP addresses and ports
- Protocol information (TCP, UDP, DNS, HTTP, HTTPS)
- Packet metadata and timing information

**Technology Stack:**
- .NET 8 Host Builder pattern
- Dependency Injection with Microsoft.Extensions.Hosting
- SharpPcap for packet capture
- PacketDotNet for packet parsing
- Kafka producer for event streaming
- PostgreSQL for persistence

## Getting Started

### Prerequisites
- Linux, macOS, or Windows with .NET 8 SDK
- Network promiscuous mode access (root/admin)
- Docker (optional, for Kafka and database)

### Build & Run

```bash
cd sakin-core/services/network-sensor
dotnet restore
dotnet build
dotnet run
```

**Note:** Network packet capture requires elevated privileges:
```bash
# Linux/macOS
sudo dotnet run

# Windows (run PowerShell as Administrator)
dotnet run
```

### Configuration

#### appsettings.json

```json
{
  "NetworkCapture": {
    "Enabled": true,
    "Interfaces": ["eth0"],
    "BpfFilter": "tcp or udp",
    "PacketBufferSize": 1000,
    "SnapshotLength": 65535
  },
  "Parsers": {
    "Http": {
      "Enabled": true,
      "ExtractPayload": false,
      "Port": 80
    },
    "Tls": {
      "Enabled": true,
      "ExtractCertificates": true,
      "Port": 443
    },
    "Dns": {
      "Enabled": true,
      "Port": 53
    }
  },
  "Kafka": {
    "BootstrapServers": "kafka:9092",
    "Topic": "raw-events",
    "BatchSize": 100,
    "FlushIntervalMs": 5000
  },
  "Database": {
    "ConnectionString": "Server=postgres;Database=network_db;User Id=postgres;Password=password;"
  }
}
```

#### Environment Variables

```bash
# Network capture
NetworkCapture__Interfaces=eth0,eth1
NetworkCapture__BpfFilter="tcp port 80 or tcp port 443"

# Kafka
Kafka__BootstrapServers=kafka:9092
Kafka__Topic=raw-events

# Database
Database__ConnectionString="Server=postgres;Database=network_db;..."

# Logging
ASPNETCORE_ENVIRONMENT=Development
Serilog__MinimumLevel=Information
```

### Packet Filtering (BPF)

BPF expressions allow filtering which packets to capture:

```bash
# Capture HTTP and HTTPS traffic
"tcp port 80 or tcp port 443"

# Capture DNS queries
"udp port 53"

# Capture all TCP traffic
"tcp"

# Exclude specific IPs
"tcp and not src 192.168.1.1"

# Capture traffic to/from specific subnet
"ip src/dst 10.0.0.0/8"
```

## Data Flow

```
1. Packet Capture
   - Network interfaces monitored in promiscuous mode
   - Packets matched against BPF filter

2. Protocol Parsing
   - Layer 4: TCP/UDP extraction
   - Layer 7: HTTP, TLS, DNS parsing
   - SNI extraction from TLS handshake
   - URL extraction from HTTP headers

3. Event Creation
   - Raw network data → NetworkEvent
   - EventEnvelope wrapping
   - Source/destination IP normalization

4. Message Publishing
   - Events serialized to JSON
   - Published to Kafka (raw-events)
   - Optionally persisted to PostgreSQL

5. Downstream Processing
   - Ingest service normalizes events
   - GeoIP enrichment applied
   - Events routed to correlation engine
```

## Development

### Building

```bash
dotnet build sakin-core/services/network-sensor/Sakin.NetworkSensor.csproj
```

### Testing

```bash
dotnet test tests/Sakin.Core.Tests/Sakin.Core.Tests.csproj
```

### Running Tests Locally

```bash
cd deployments
docker compose -f docker-compose.dev.yml up -d postgres redis kafka

# In another terminal
cd sakin-core/services/network-sensor
dotnet test
```

## Performance Characteristics

- **Packet Capture Rate**: 100,000+ packets/second (depends on hardware)
- **CPU Usage**: <10% per core at 1000 EPS
- **Memory**: ~100MB baseline + buffers
- **Latency**: <1ms from packet capture to Kafka publishing

## Deployment

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app
COPY bin/Release/net8.0 .

RUN apt-get update && apt-get install -y libpcap0.8
ENV NetworkCapture__Interfaces=eth0

ENTRYPOINT ["dotnet", "Sakin.NetworkSensor.dll"]
```

### Kubernetes

See [Deployment Guide](../../docs/deployment.md) for Helm charts and K8s manifests.

## Integration

### Output
- **Kafka Topic**: `raw-events`
- **Format**: EventEnvelope with NetworkEvent payload
- **Fields**: SourceIp, DestinationIp, SourcePort, DestinationPort, Protocol, Urls, Hostname, etc.

### Downstream Services
- **Ingest Service**: Normalization and enrichment
- **Correlation Engine**: Threat detection rules
- **SOAR**: Automated response
- **Panel**: Visualization and alerting

## Monitoring

### Metrics
- `network_packets_captured_total`: Total packets captured
- `network_packets_dropped_total`: Dropped packets
- `network_events_published_total`: Events published to Kafka
- `network_parser_errors_total`: Parsing errors
- `network_capture_duration_seconds`: Capture processing time

### Health Checks
- `GET /healthz`: Service health status
- Interface availability
- Kafka connectivity
- BPF filter validation

### Logs
Structured JSON logs via Serilog:
- Packet capture start/stop
- Parser errors and statistics
- Kafka publishing
- Performance metrics

## Troubleshooting

### No Packets Captured
1. Verify network interface name: `ip link show`
2. Check promiscuous mode: `ip link set dev eth0 promisc on`
3. Verify BPF filter syntax
4. Check firewall/network policies
5. Review system logs: `dmesg`

### High Packet Loss
1. Increase buffer size: `NetworkCapture__PacketBufferSize`
2. Reduce BPF filter scope if possible
3. Check system load: `top`
4. Monitor network throughput: `iftop`

### Kafka Connection Issues
1. Verify broker connectivity: `telnet kafka 9092`
2. Check bootstrap servers configuration
3. Verify Kafka topic exists: `kafka-topics.sh --list`
4. Review network policies and firewall

### Memory Usage High
1. Check packet buffer size
2. Monitor Kafka producer batching
3. Review long-running connections
4. Check for memory leaks in logs

## Further Reading

- [Architecture Overview](../../docs/architecture.md)
- [Event Schema](../../docs/event-schema.md)
- [Deployment Guide](../../docs/deployment.md)
- [Monitoring Guide](../../docs/monitoring.md)
