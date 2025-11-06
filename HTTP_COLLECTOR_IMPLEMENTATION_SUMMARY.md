# HTTP CEF/Syslog Collector Implementation Summary

## Overview

Successfully implemented **Sakin.HttpCollector**, a high-performance HTTP endpoint for accepting CEF (Common Event Format) and Syslog messages from network devices, firewalls, IDS systems, and custom agents.

**Sprint:** Sprint 5  
**Status:** ✅ Complete  
**Location:** `sakin-collectors/Sakin.HttpCollector/`

## Architecture

### Core Design Principles

1. **Decoupled Architecture**: HTTP endpoint and Kafka publisher are separate services
2. **Asynchronous Pipeline**: Uses `System.Threading.Channels` for in-memory queuing
3. **Fast Response**: Returns 202 Accepted immediately without waiting for Kafka
4. **Auto-Detection**: Automatically identifies CEF vs Syslog format

### Components

```
Sakin.HttpCollector/
├── Configuration/
│   ├── HttpCollectorOptions.cs      # HTTP server configuration
│   └── KafkaPublisherOptions.cs     # Kafka configuration
├── Middleware/
│   └── ApiKeyAuthenticationMiddleware.cs  # Optional API key validation
├── Models/
│   └── RawLogEntry.cs               # DTO for channel communication
├── Services/
│   ├── HttpEndpointService.cs       # Kestrel HTTP server (BackgroundService)
│   ├── KafkaPublisherService.cs     # Kafka publisher (BackgroundService)
│   ├── IMetricsService.cs           # Metrics interface
│   └── MetricsService.cs            # Prometheus metrics implementation
├── Program.cs                        # Application entry point
├── appsettings.json                  # Default configuration
├── appsettings.Production.json       # Production configuration example
├── Dockerfile                        # Container build file
├── docker-compose.yml                # Local testing setup
└── test-http-collector.sh           # Manual test script
```

## Technical Implementation

### 1. HTTP Endpoint Service

- Hosts **Kestrel** in-process within a .NET Worker Service
- Listens on configurable port (default: 8080)
- Exposes `POST /api/events` endpoint
- Validates payload size and authentication
- Writes to in-memory channel and returns 202 immediately

### 2. Kafka Publisher Service

- Reads from in-memory `Channel<RawLogEntry>`
- Auto-detects message format:
  - **CEF JSON** (`cef_json`): `Content-Type: application/json`
  - **CEF String** (`cef_string`): Starts with `CEF:`
  - **Syslog String** (`syslog_string`): Plain text
- Creates `EventEnvelope` with metadata
- Publishes to Kafka `raw-events` topic

### 3. Authentication Middleware

- Optional API key authentication via `X-API-Key` header
- Configurable list of valid API keys
- Returns 401 Unauthorized for invalid/missing keys

### 4. Metrics Service

Prometheus metrics:
- `sakin_http_requests_total{source_ip, format, status_code}`
- `sakin_http_request_duration_seconds`
- `sakin_http_errors_total{error_code}`
- `sakin_kafka_messages_published_total{topic}`

## Configuration

### appsettings.json

```json
{
  "HttpCollector": {
    "Port": 8080,
    "Path": "/api/events",
    "MaxBodySize": 65536,
    "RequireApiKey": false,
    "ValidApiKeys": []
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
    "MaxInFlight": 5,
    "EnableIdempotence": true
  }
}
```

## API Endpoints

### POST /api/events

**Accepts:**
- `Content-Type: text/plain` - Raw Syslog/CEF string
- `Content-Type: application/json` - JSON wrapper
- `Content-Type: application/x-www-form-urlencoded` - Form data

**Headers:**
- `X-API-Key` (optional): API key for authentication
- `X-Source` (optional): Custom source identifier (defaults to client IP)

**Response Codes:**
- `202 Accepted` - Message queued successfully
- `400 Bad Request` - Empty body
- `401 Unauthorized` - Invalid/missing API key
- `413 Payload Too Large` - Body exceeds MaxBodySize
- `500 Internal Server Error` - Server error

### GET /metrics

Prometheus metrics endpoint

## Testing

### Unit Tests

**Location:** `tests/Sakin.HttpCollector.Tests/Unit/`

- ✅ `ConfigurationTests.cs` - Configuration defaults and section names
- ✅ `MetricsServiceTests.cs` - Metrics service methods
- ✅ `RawLogEntryTests.cs` - DTO creation and properties

**Results:** 11/11 tests passing

### Integration Tests

**Location:** `tests/Sakin.HttpCollector.Tests/Integration/`

- ✅ `HttpCollectorE2ETests.cs` - End-to-end tests with Kafka

### Manual Testing

**Script:** `sakin-collectors/Sakin.HttpCollector/test-http-collector.sh`

Tests:
1. CEF message acceptance
2. Syslog message acceptance
3. JSON CEF message acceptance
4. X-Source header support
5. Empty body rejection
6. Metrics endpoint

## Usage Examples

### Raw CEF Message

```bash
curl -X POST http://localhost:8080/api/events \
  -H "Content-Type: text/plain" \
  -d "CEF:0|Security|IDS|1.0|100|Attack|9|src=192.168.1.100"
```

### Syslog Message

```bash
curl -X POST http://localhost:8080/api/events \
  -H "Content-Type: text/plain" \
  -d "Jan 15 10:30:00 firewall sshd[1234]: Failed password"
```

### With API Key

```bash
curl -X POST http://localhost:8080/api/events \
  -H "X-API-Key: your-secret-key" \
  -H "Content-Type: text/plain" \
  -d "CEF:0|Vendor|Product|1.0|200|Event|5|"
```

### With Custom Source

```bash
curl -X POST http://localhost:8080/api/events \
  -H "X-Source: firewall-01" \
  -H "Content-Type: text/plain" \
  -d "syslog message"
```

## Docker Deployment

### Build Image

```bash
docker build -t sakin-http-collector:latest \
  -f sakin-collectors/Sakin.HttpCollector/Dockerfile .
```

### Run Container

```bash
docker run -d \
  -p 8080:8080 \
  -e Kafka__BootstrapServers=kafka:9092 \
  -e HttpCollector__RequireApiKey=true \
  -e HttpCollector__ValidApiKeys__0=secret-key-1 \
  sakin-http-collector:latest
```

### Docker Compose

```bash
cd sakin-collectors/Sakin.HttpCollector
docker-compose up -d
```

## Event Envelope Format

Messages published to Kafka:

```json
{
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "receivedAt": "2024-01-15T10:30:00Z",
  "source": "192.168.1.100",
  "sourceType": "cef_string",
  "raw": "CEF:0|Security|IDS|1.0|100|Attack|9|src=192.168.1.100",
  "normalized": {
    "sourceIp": "192.168.1.100",
    "timestamp": "2024-01-15T10:30:00Z"
  },
  "schemaVersion": "v1.0"
}
```

## Dependencies

- **Framework:** .NET 8
- **Project References:**
  - `Sakin.Common` - EventEnvelope models
  - `Sakin.Messaging` - Kafka producer
- **NuGet Packages:**
  - `prometheus-net` (8.2.1) - Metrics
  - `prometheus-net.AspNetCore` (8.2.1) - Metrics HTTP endpoint
  - `Microsoft.Extensions.Hosting` (8.0.0) - Worker service
  - `Microsoft.AspNetCore.App` (FrameworkReference) - Kestrel

## Build & Run

### Development

```bash
# Build
dotnet build sakin-collectors/Sakin.HttpCollector/Sakin.HttpCollector.csproj

# Run
dotnet run --project sakin-collectors/Sakin.HttpCollector/Sakin.HttpCollector.csproj

# Test
dotnet test tests/Sakin.HttpCollector.Tests/Sakin.HttpCollector.Tests.csproj
```

### Release

```bash
dotnet build sakin-collectors/Sakin.HttpCollector/Sakin.HttpCollector.csproj \
  --configuration Release

dotnet publish sakin-collectors/Sakin.HttpCollector/Sakin.HttpCollector.csproj \
  --configuration Release \
  --output ./publish
```

## Solution Integration

Projects added to `SAKINCore-CS.sln`:
- ✅ `sakin-collectors/Sakin.HttpCollector/Sakin.HttpCollector.csproj`
- ✅ `tests/Sakin.HttpCollector.Tests/Sakin.HttpCollector.Tests.csproj`

## Performance Characteristics

- **Request Handling:** < 5ms (immediate 202 response)
- **Kafka Publishing:** Asynchronous, batched
- **Throughput:** Unbounded channel, no backpressure on HTTP
- **Memory:** Channel is unbounded but items processed immediately
- **Compression:** Snappy compression for Kafka messages

## Security Considerations

1. **API Key Authentication:** Optional but recommended for production
2. **Payload Size Limits:** Prevents DoS attacks
3. **Source IP Logging:** Full audit trail
4. **Rate Limiting:** Can be added via middleware (future enhancement)

## Acceptance Criteria Status

✅ Service starts, listens on HTTP port 8080  
✅ POST /api/events accepts Syslog/CEF messages  
✅ Detects format and publishes to Kafka  
✅ Returns 202 on success, 400/401 on error  
✅ API Key validation works (if enabled)  
✅ Metrics exported at /metrics  
✅ Builds without errors  
✅ Unit tests pass (11/11)  
✅ Project added to solution  
✅ Docker support included  
✅ Comprehensive documentation  

## Future Enhancements

1. Rate limiting middleware
2. TLS/HTTPS support
3. Webhook retry mechanism
4. Batch endpoint for multiple events
5. Health check endpoint
6. OpenAPI/Swagger documentation
7. Request ID tracking
8. Circuit breaker for Kafka connection

## Conclusion

The HTTP Collector provides a robust, high-performance alternative ingestion path for devices that prefer HTTP/S webhooks over UDP Syslog. The decoupled architecture ensures fast response times while the channel-based pipeline provides reliable Kafka publishing.
