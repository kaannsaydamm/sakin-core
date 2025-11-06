# Sakin Collectors

## Overview
Data collection agents and plugins for gathering security-relevant information from various sources.

## Purpose
This directory will contain specialized collectors that extend beyond network packet capture to gather data from:
- System logs and events
- Endpoint security sensors
- Cloud platform APIs (AWS, Azure, GCP)
- Application logs
- Security appliances and tools
- Third-party security feeds

## Status
✅ **Windows EventLog collector** (`Sakin.Agents.Windows`) is available and ready to forward Windows security, system, and application events into the Sakin pipeline.
🚧 Additional collectors remain under development.

## Windows EventLog Collector
- Targets Security, System, and Application logs with configurable enable/disable per log
- Filters high-value security events (4624, 4625, 4768, 4769) and critical system warnings/errors by default
- Publishes events to Kafka using the shared `Sakin.Messaging` producer and the standard `EventEnvelope` format
- Supports real-time subscription and batch polling modes with graceful shutdown and flush logic

### Configuration
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

## Planned Features
- Modular collector framework
- Plugin architecture for extensibility
- Standardized data output format
- Configuration management for multiple collectors
- Rate limiting and backpressure handling

## Integration
Collectors feed data to the sakin-ingest layer for normalization and processing.
