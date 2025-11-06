# Sakin Collectors

## Overview
Data collection agents and plugins for gathering security-relevant information from various sources.

## Purpose
This directory contains specialized collectors that extend beyond network packet capture to gather data from:
- System logs and events
- Endpoint security sensors
- Cloud platform APIs (AWS, Azure, GCP)
- Application logs
- Security appliances and tools
- Third-party security feeds

## Available Collectors

### ✅ Windows EventLog Collector (`Sakin.Agents.Windows`)
- Targets Security, System, and Application logs with configurable enable/disable per log
- Filters high-value security events (4624, 4625, 4768, 4769) and critical system warnings/errors by default
- Publishes events to Kafka using the shared `Sakin.Messaging` producer and the standard `EventEnvelope` format
- Supports real-time subscription and batch polling modes with graceful shutdown and flush logic

#### Configuration
Sample `appsettings.json`:
```json
{
  "Agent": {
    "Name": "eventlog-collector-01",
    "Hostname": "server01"
  },
  "EventLogs": {
    "Enabled": true,
    "Mode": "RealTime",
    "PollInterval": 5000,
    "BatchSize": 100,
    "LogNames": ["Security", "System", "Application"]
  },
  "Kafka": {
    "Enabled": true,
    "BootstrapServers": "kafka:9092",
    "Topic": "raw-events",
    "BatchSize": 100,
    "FlushIntervalMs": 5000
  }
}
```

### ✅ Syslog Listener (`Sakin.Syslog`)
- UDP listener on configurable port (default: 514)
- Optional TCP listener on configurable port (default: 514)
- RFC5424 and RFC3164 syslog format support
- Graceful handling of malformed messages
- Async batch publishing to Kafka
- Resilience with retry logic and backpressure handling

#### Configuration
Sample `appsettings.json`:
```json
{
  "Agent": {
    "Name": "syslog-collector-01",
    "Hostname": "localhost"
  },
  "Syslog": {
    "UdpPort": 514,
    "TcpPort": 514,
    "TcpEnabled": false,
    "BufferSize": 65535
  },
  "Kafka": {
    "Enabled": true,
    "BootstrapServers": "kafka:9092",
    "Topic": "raw-events",
    "BatchSize": 100,
    "FlushIntervalMs": 5000,
    "RetryCount": 3,
    "RetryBackoffMs": 500
  }
}
```

#### Testing
```bash
# Send test syslog message via UDP
echo "<34>Oct 11 22:14:15 mymachine su: 'su root' failed for lonvick" | nc -u localhost 514

# Send test syslog message via TCP (if enabled)
echo "<34>Oct 11 22:14:15 mymachine su: 'su root' failed for lonvick" | nc localhost 514
```

## Planned Features
- Additional collectors for Linux system logs
- Cloud platform API collectors
- Application log collectors
- Modular collector framework
- Plugin architecture for extensibility
- Standardized data output format
- Configuration management for multiple collectors
- Rate limiting and backpressure handling

## Integration
All collectors feed data to the sakin-ingest layer for normalization and processing using the standard `EventEnvelope` format.
