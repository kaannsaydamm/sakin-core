# Sakin Windows EventLog Agent

The **Sakin Windows EventLog Agent** runs as a .NET 8 worker service (Windows Service compatible) and streams Windows EventLog records into the Sakin platform via Kafka.

## Installation

### Quick Install (Recommended)

1. Download the installer from [GitHub Releases](https://github.com/sakin-security/sakin-core/releases)
2. Run `sakin-agent-windows-setup.exe` as Administrator
3. Follow the installation wizard
4. Enter your SAKIN endpoint and authentication token

See [Windows Installation Guide](https://github.com/sakin-security/sakin-core/blob/main/packaging/install-windows-README.md) for detailed instructions.

### Silent Installation

```powershell
.\sakin-agent-windows-setup.exe /S /endpoint=http://sakin-server:5001 /token=your-token
```

### Startup Behavior

The agent is installed as a Windows Service with:
- **Startup Type**: Automatic (starts on system boot)
- **Service Name**: `sakin-agent-windows`
- **Display Name**: `SAKIN Agent`
- **Log Source**: `SAKIN Agent` in Windows Application Event Log

## Features

- Monitors Security, System, and Application logs (configurable)
- Filters high-value security event IDs (4624, 4625, 4768, 4769) by default
- Supports real-time subscriptions and scheduled batch polling
- Publishes events to Kafka using the shared `Sakin.Messaging` library and the standard `EventEnvelope` schema
- Batching with queue-based flush (100 events or 5 seconds by default)
- Graceful shutdown with queue flush and watcher disposal

## Configuration

The agent uses `appsettings.json` (or environment variables) for configuration. Key sections:

```json
{
  "Agent": {
    "Name": "eventlog-collector-01",
    "Hostname": "server01"
  },
  "Sakin": {
    "IngestEndpoint": "http://localhost:5001",
    "AgentToken": "your-token"
  },
  "EventLogs": {
    "Enabled": true,
    "Mode": "RealTime", // or "Batch"
    "PollInterval": 5000,
    "BatchSize": 100,
    "LogNames": ["Security", "System", "Application"],
    "Logs": [
      { "Name": "Security", "Enabled": true, "EventIds": [4624, 4625, 4768, 4769] },
      { "Name": "System", "Enabled": true },
      { "Name": "Application", "Enabled": true }
    ]
  },
  "Kafka": {
    "Enabled": true,
    "BootstrapServers": "kafka:9092",
    "Topic": "raw-events",
    "BatchSize": 100,
    "FlushIntervalMs": 5000,
    "RetryCount": 3,
    "RetryBackoffMs": 500
  },
  "KafkaProducer": {
    "DefaultTopic": "raw-events"
  }
}
```

- **Mode**: `RealTime` uses `EventLogWatcher` subscriptions; `Batch` polls on the configured interval.
- **Logs**: `LogNames` provide a quick allow-list; `Logs` enables per-log overrides (enable/disable, explicit event IDs, or custom query strings).

## Running locally

```bash
# Restore dependencies
 dotnet restore

# Build the agent
 dotnet build Sakin.Agents.Windows.csproj

# Run (interactive console)
 dotnet run --project Sakin.Agents.Windows.csproj
```

On Windows servers the binary can be installed as a Windows Service using the standard `sc create` flow or with PowerShell `New-Service`, leveraging the included Windows Service lifetime integration.

## Graceful shutdown

The agent listens for termination signals. On shutdown it:

1. Stops event log watchers or polling loops
2. Flushes any queued events to Kafka
3. Disposes watchers and Kafka resources

This ensures no event loss during redeployments or service restarts.
