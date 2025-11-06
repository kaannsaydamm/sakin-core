# Sakin.HttpCollector

## Overview

Sakin.HttpCollector is a lightweight, high-performance HTTP endpoint designed to accept CEF (Common Event Format) and Syslog messages from network devices, firewalls, IDS systems, and custom agents that prefer HTTP/S webhooks over UDP Syslog.

This collector is part of Sprint 5 of the S.A.K.I.N. SIEM project and provides a crucial alternative ingestion path to the existing Kafka pipeline.

## Architecture

The service uses a decoupled architecture with two primary components:

1. **HTTP Endpoint Service**: Accepts incoming HTTP POST requests and immediately returns 202 Accepted after writing to an in-memory channel
2. **Kafka Publisher Service**: Reads from the channel, detects format, creates EventEnvelope objects, and publishes to Kafka

This design ensures ultra-fast request handling without waiting for Kafka publishing.

## Features

- ✅ Lightweight .NET 8 Worker Service with embedded Kestrel
- ✅ Fast asynchronous ingestion pipeline using System.Threading.Channels
- ✅ Auto-detection of CEF vs Syslog format
- ✅ Optional API key authentication
- ✅ Payload size validation
- ✅ Prometheus metrics
- ✅ EventEnvelope creation with metadata

## Endpoints

### POST /api/events

Accepts CEF and Syslog messages.

**Supported Content-Types:**
- `text/plain` - Raw Syslog or CEF string
- `application/json` - JSON wrapper with message field
- `application/x-www-form-urlencoded` - Form-encoded data

**Headers:**
- `X-API-Key` (optional): API key for authentication
- `X-Source` (optional): Custom source identifier (defaults to client IP)
- `Content-Type`: Message format

**Request Examples:**

```bash
# Raw Syslog
curl -X POST http://localhost:8080/api/events \
  -H "Content-Type: text/plain" \
  -d "Jan 15 10:30:00 firewall sshd[1234]: Failed password for admin from 192.168.1.100"

# CEF Format
curl -X POST http://localhost:8080/api/events \
  -H "Content-Type: text/plain" \
  -d "CEF:0|Vendor|Product|Version|SignatureID|Name|Severity|src=192.168.1.100 dst=10.0.0.1"

# JSON Wrapper
curl -X POST http://localhost:8080/api/events \
  -H "Content-Type: application/json" \
  -d '{"message":"CEF:0|...", "source_ip":"192.168.1.100"}'

# With API Key
curl -X POST http://localhost:8080/api/events \
  -H "X-API-Key: your-secret-key" \
  -H "Content-Type: text/plain" \
  -d "syslog message"
```

**Response Codes:**
- `202 Accepted` - Message accepted and queued for processing
- `400 Bad Request` - Empty body or malformed request
- `401 Unauthorized` - Missing or invalid API key
- `413 Payload Too Large` - Body exceeds MaxBodySize limit
- `500 Internal Server Error` - Server error

## Configuration

Configuration is managed through `appsettings.json`:

```json
{
  "HttpCollector": {
    "Port": 8080,
    "Path": "/api/events",
    "MaxBodySize": 65536,
    "RequireApiKey": false,
    "ValidApiKeys": ["key1", "key2"]
  },
  "Kafka": {
    "BootstrapServers": "kafka:9092",
    "Topic": "raw-events"
  },
  "KafkaOptions": {
    "BootstrapServers": "kafka:9092"
  },
  "ProducerOptions": {
    "LingerMs": 10,
    "BatchSize": 16384,
    "CompressionType": "snappy",
    "Acks": "all",
    "EnableIdempotence": true
  }
}
```

### Configuration Options

**HttpCollector:**
- `Port`: HTTP port to listen on (default: 8080)
- `Path`: Endpoint path (default: /api/events)
- `MaxBodySize`: Maximum request body size in bytes (default: 65536)
- `RequireApiKey`: Enable API key authentication (default: false)
- `ValidApiKeys`: Array of valid API keys

**Kafka:**
- `BootstrapServers`: Kafka broker addresses
- `Topic`: Target Kafka topic for raw events

## Metrics

The service exposes Prometheus metrics at `/metrics`:

- `sakin_http_requests_total{source_ip, format, status_code}` - Total HTTP requests
- `sakin_http_request_duration_seconds` - Request duration histogram
- `sakin_http_errors_total{error_code}` - Total HTTP errors
- `sakin_kafka_messages_published_total{topic}` - Messages published to Kafka

## Building and Running

### Local Development

```bash
# Build
dotnet build sakin-collectors/Sakin.HttpCollector/Sakin.HttpCollector.csproj

# Run
dotnet run --project sakin-collectors/Sakin.HttpCollector/Sakin.HttpCollector.csproj
```

### Docker

```bash
# Build image
docker build -t sakin-http-collector:latest -f sakin-collectors/Sakin.HttpCollector/Dockerfile .

# Run container
docker run -p 8080:8080 -e Kafka__BootstrapServers=kafka:9092 sakin-http-collector:latest
```

## Format Detection

The service automatically detects the message format:

1. **CEF JSON** (`cef_json`): Content-Type contains `application/json`
2. **CEF String** (`cef_string`): Content-Type is `text/plain` and message starts with `CEF:`
3. **Syslog String** (`syslog_string`): Content-Type is `text/plain` and doesn't match CEF pattern

## Event Envelope

All messages are wrapped in an EventEnvelope before being published to Kafka:

```json
{
  "eventId": "guid",
  "receivedAt": "2024-01-15T10:30:00Z",
  "source": "192.168.1.100",
  "sourceType": "cef_string",
  "raw": "CEF:0|...",
  "normalized": {
    "sourceIp": "192.168.1.100",
    "timestamp": "2024-01-15T10:30:00Z"
  },
  "schemaVersion": "v1.0"
}
```

## Performance

- Asynchronous request handling with immediate 202 response
- Decoupled Kafka publishing via unbounded channel
- No blocking on Kafka operations
- Batched Kafka publishing with configurable parameters

## Security

- Optional API key authentication via X-API-Key header
- Payload size limits to prevent abuse
- Source IP logging for audit trail
- Rate limiting can be added via middleware

## Dependencies

- .NET 8
- Sakin.Common (EventEnvelope models)
- Sakin.Messaging (Kafka producer)
- prometheus-net (Metrics)
- Microsoft.AspNetCore.App (Kestrel)
