# Sakin Syslog Agent

A .NET 8 worker service that listens for syslog messages on UDP/TCP ports and publishes them to Kafka.

## Features

- UDP listener on configurable port (default: 514)
- Optional TCP listener on configurable port (default: 514)
- RFC5424 and RFC3164 syslog format support
- Graceful handling of malformed messages
- Async batch publishing to Kafka
- Resilience with retry logic and backpressure handling
- Configurable buffer sizes and batch processing

## Configuration

The service is configured via `appsettings.json`:

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
    "ClientId": "syslog-collector-01",
    "Topic": "raw-events",
    "BatchSize": 100,
    "FlushIntervalMs": 5000,
    "RetryCount": 3,
    "RetryBackoffMs": 500
  }
}
```

### Configuration Options

- **Agent.Name**: Unique identifier for this collector instance
- **Agent.Hostname**: Hostname to use as source in events
- **Syslog.UdpPort**: UDP port to listen on (default: 514)
- **Syslog.TcpPort**: TCP port to listen on (default: 514)
- **Syslog.TcpEnabled**: Whether to enable TCP listener (default: false)
- **Syslog.BufferSize**: Buffer size for receiving messages (default: 65535)

## Building and Running

### Build

```bash
dotnet build Sakin.Syslog.csproj
```

### Run locally

```bash
dotnet run
```

### Run as Docker service

```bash
docker build -t sakin-syslog .
docker run -p 514:514/udp -p 514:514 sakin-syslog
```

## Testing

### Send test syslog message via UDP

```bash
echo "<34>Oct 11 22:14:15 mymachine su: 'su root' failed for lonvick on /dev/pts/8" | nc -u localhost 514
```

### Send test syslog message via TCP (if enabled)

```bash
echo "<34>Oct 11 22:14:15 mymachine su: 'su root' failed for lonvick on /dev/pts/8" | nc localhost 514
```

## Event Format

Syslog messages are converted to `EventEnvelope` format with:

- **SourceType**: "syslog"
- **Source**: Configured hostname or syslog hostname
- **Raw**: Complete raw syslog message
- **Normalized**: Parsed fields (timestamp, hostname, tag, severity, etc.)
- **Enrichment**: Syslog-specific fields (facility, severity, priority, tag)

## Supported Formats

- **RFC5424**: Structured syslog with timestamp, hostname, app-name, procid, msgid, structured-data
- **RFC3164**: Traditional BSD syslog format
- **Simple**: Basic messages with optional priority field

## Error Handling

- Malformed messages are logged but don't crash the service
- Failed Kafka publishes are retried with exponential backoff
- Burst handling with queue-based batching
- Graceful shutdown with message flushing