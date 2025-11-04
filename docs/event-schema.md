# SAKIN Event Schema Documentation

## Overview

The SAKIN Event Schema defines a normalized data structure for security monitoring events across the entire platform. This schema ensures consistency, interoperability, and proper data validation across all services.

## Design Principles

1. **Extensibility**: Base event types can be extended for specific use cases
2. **Immutability**: Using C# record types with init-only properties
3. **Validation**: JSON Schema validation ensures data integrity
4. **Interoperability**: Standard JSON serialization for cross-service communication
5. **Type Safety**: Strong typing in C# with comprehensive enum definitions

## Schema Location

- **JSON Schema**: `/schema/event-schema.json`
- **C# Models**: `/sakin-utils/Sakin.Common/Models/`
- **Validation**: `/sakin-utils/Sakin.Common/Validation/EventValidator.cs`

## Base Event Type: NormalizedEvent

The `NormalizedEvent` is the base record type for all events in the system.

### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `id` | Guid | Yes | Unique identifier for the event (auto-generated) |
| `timestamp` | DateTime | Yes | UTC timestamp when the event occurred (auto-generated) |
| `eventType` | EventType | Yes | Type of event (enum) |
| `severity` | Severity | Yes | Severity level of the event (enum) |
| `sourceIp` | string | Yes | Source IP address (IPv4 or IPv6) |
| `destinationIp` | string | Yes | Destination IP address (IPv4 or IPv6) |
| `sourcePort` | int? | No | Source port number (0-65535) |
| `destinationPort` | int? | No | Destination port number (0-65535) |
| `protocol` | Protocol | Yes | Network protocol (enum) |
| `payload` | string? | No | Event payload or additional details |
| `metadata` | Dictionary<string, object> | No | Additional metadata as key-value pairs |
| `deviceName` | string? | No | Name of the device/sensor that captured the event |
| `sensorId` | string? | No | Unique identifier of the sensor |

### C# Definition

```csharp
public record NormalizedEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public EventType EventType { get; init; } = EventType.Unknown;
    public Severity Severity { get; init; } = Severity.Info;
    public string SourceIp { get; init; } = string.Empty;
    public string DestinationIp { get; init; } = string.Empty;
    public int? SourcePort { get; init; }
    public int? DestinationPort { get; init; }
    public Protocol Protocol { get; init; } = Protocol.Unknown;
    public string? Payload { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public string? DeviceName { get; init; }
    public string? SensorId { get; init; }
}
```

### JSON Example

```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "timestamp": "2024-11-04T10:30:00Z",
  "eventType": "networkTraffic",
  "severity": "info",
  "sourceIp": "192.168.1.100",
  "destinationIp": "8.8.8.8",
  "sourcePort": 54321,
  "destinationPort": 443,
  "protocol": "https",
  "payload": null,
  "metadata": {},
  "deviceName": "eth0",
  "sensorId": "sensor-01"
}
```

**Note**: Enum values in JSON use camelCase formatting (e.g., `networkTraffic`, not `NetworkTraffic`).

## Specialized Event: NetworkEvent

The `NetworkEvent` extends `NormalizedEvent` with network-specific properties.

### Additional Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `bytesSent` | long | No | Number of bytes sent |
| `bytesReceived` | long | No | Number of bytes received |
| `packetCount` | int | No | Total packet count |
| `sni` | string? | No | Server Name Indication from TLS handshake |
| `httpUrl` | string? | No | HTTP/HTTPS URL |
| `httpMethod` | string? | No | HTTP request method (GET, POST, etc.) |
| `httpStatusCode` | int? | No | HTTP response status code (100-599) |
| `userAgent` | string? | No | HTTP User-Agent header |

### C# Definition

```csharp
public record NetworkEvent : NormalizedEvent
{
    public NetworkEvent()
    {
        EventType = EventType.NetworkTraffic;
    }

    public long BytesSent { get; init; }
    public long BytesReceived { get; init; }
    public int PacketCount { get; init; }
    public string? Sni { get; init; }
    public string? HttpUrl { get; init; }
    public string? HttpMethod { get; init; }
    public int? HttpStatusCode { get; init; }
    public string? UserAgent { get; init; }
}
```

### JSON Example

```json
{
  "id": "223e4567-e89b-12d3-a456-426614174001",
  "timestamp": "2024-11-04T10:30:15Z",
  "eventType": "networkTraffic",
  "severity": "info",
  "sourceIp": "192.168.1.100",
  "destinationIp": "93.184.216.34",
  "sourcePort": 54322,
  "destinationPort": 443,
  "protocol": "https",
  "bytesSent": 1024,
  "bytesReceived": 4096,
  "packetCount": 15,
  "sni": "example.com",
  "httpUrl": "https://example.com/api/data",
  "httpMethod": "GET",
  "httpStatusCode": 200,
  "userAgent": "Mozilla/5.0",
  "metadata": {
    "region": "us-east-1",
    "environment": "production"
  },
  "deviceName": "eth0",
  "sensorId": "sensor-01"
}
```

## Enumerations

### EventType

Defines the type of security event:

| Value | Description |
|-------|-------------|
| `Unknown` (0) | Unknown or unclassified event |
| `NetworkTraffic` (1) | General network traffic event |
| `DnsQuery` (2) | DNS query event |
| `HttpRequest` (3) | HTTP/HTTPS request event |
| `TlsHandshake` (4) | TLS handshake event |
| `SshConnection` (5) | SSH connection event |
| `FileAccess` (6) | File access event |
| `ProcessExecution` (7) | Process execution event |
| `AuthenticationAttempt` (8) | Authentication attempt event |
| `SystemLog` (9) | System log event |
| `SecurityAlert` (10) | Security alert event |

### Severity

Defines the severity level of an event:

| Value | Description |
|-------|-------------|
| `Unknown` (0) | Unknown severity |
| `Info` (1) | Informational event |
| `Low` (2) | Low severity event |
| `Medium` (3) | Medium severity event |
| `High` (4) | High severity event |
| `Critical` (5) | Critical severity event |

### Protocol

Defines network protocols:

| Value | Description |
|-------|-------------|
| `Unknown` (0) | Unknown protocol |
| `TCP` (1) | Transmission Control Protocol |
| `UDP` (2) | User Datagram Protocol |
| `ICMP` (3) | Internet Control Message Protocol |
| `HTTP` (4) | Hypertext Transfer Protocol |
| `HTTPS` (5) | HTTP Secure |
| `DNS` (6) | Domain Name System |
| `SSH` (7) | Secure Shell |
| `FTP` (8) | File Transfer Protocol |
| `SMTP` (9) | Simple Mail Transfer Protocol |
| `TLS` (10) | Transport Layer Security |

## Validation

### Using EventValidator

The `EventValidator` class provides JSON Schema validation for events:

```csharp
using Sakin.Common.Validation;
using Sakin.Common.Models;

// Load validator with schema
var validator = EventValidator.FromFile("/path/to/event-schema.json");

// Validate an event object
var evt = new NetworkEvent
{
    SourceIp = "192.168.1.100",
    DestinationIp = "8.8.8.8",
    Protocol = Protocol.HTTPS,
    BytesSent = 1024
};

var result = validator.Validate(evt);
if (result.IsValid)
{
    Console.WriteLine("Event is valid!");
}
else
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Validation error: {error}");
    }
}

// Validate JSON string
var json = """{"id": "...", "timestamp": "...", ...}""";
var jsonResult = validator.ValidateJson(json);
```

### Serialization and Deserialization

The validator provides serialization methods that ensure consistent JSON formatting:

```csharp
// Serialize to JSON
var evt = new NetworkEvent
{
    SourceIp = "192.168.1.100",
    DestinationIp = "8.8.8.8",
    Protocol = Protocol.TCP
};

string json = validator.Serialize(evt);

// Deserialize from JSON
var deserializedEvent = validator.Deserialize<NetworkEvent>(json);

// Verify no data loss
Assert.Equal(evt.SourceIp, deserializedEvent.SourceIp);
Assert.Equal(evt.Protocol, deserializedEvent.Protocol);
```

## Validation Rules

The JSON Schema enforces the following rules:

1. **Required Fields**: `id`, `timestamp`, `eventType`, `severity`, `sourceIp`, `destinationIp`, `protocol`
2. **UUID Format**: `id` must be a valid UUID
3. **DateTime Format**: `timestamp` must be ISO 8601 format
4. **IP Address Format**: `sourceIp` and `destinationIp` must be valid IPv4 or IPv6 addresses
5. **Port Range**: Port numbers must be between 0 and 65535
6. **Enum Values**: All enum fields must match defined values
7. **HTTP Status Codes**: Must be between 100 and 599
8. **HTTP Methods**: Must be valid HTTP verbs (GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS, TRACE, CONNECT)

## Usage Examples

### Creating Events

```csharp
// Create a basic normalized event
var basicEvent = new NormalizedEvent
{
    EventType = EventType.DnsQuery,
    Severity = Severity.Info,
    SourceIp = "192.168.1.100",
    DestinationIp = "8.8.8.8",
    Protocol = Protocol.DNS,
    Payload = "example.com"
};

// Create a network event with additional properties
var networkEvent = new NetworkEvent
{
    SourceIp = "192.168.1.100",
    DestinationIp = "93.184.216.34",
    SourcePort = 54321,
    DestinationPort = 443,
    Protocol = Protocol.HTTPS,
    Severity = Severity.Info,
    BytesSent = 512,
    BytesReceived = 2048,
    PacketCount = 8,
    Sni = "example.com",
    HttpUrl = "https://example.com/api",
    HttpMethod = "GET",
    HttpStatusCode = 200,
    DeviceName = "eth0",
    SensorId = "sensor-01"
};
```

### Modifying Events (with-expressions)

Since events are record types, use `with` expressions for modifications:

```csharp
var originalEvent = new NetworkEvent
{
    SourceIp = "192.168.1.100",
    DestinationIp = "8.8.8.8",
    Protocol = Protocol.TCP
};

// Create a modified copy
var modifiedEvent = originalEvent with 
{ 
    Severity = Severity.High,
    Metadata = new Dictionary<string, object>
    {
        { "analyzed", true },
        { "threatScore", 85 }
    }
};
```

### Adding Metadata

```csharp
var evt = new NetworkEvent
{
    SourceIp = "192.168.1.100",
    DestinationIp = "8.8.8.8",
    Protocol = Protocol.TCP,
    Metadata = new Dictionary<string, object>
    {
        { "geoip_country", "US" },
        { "geoip_city", "New York" },
        { "asn", 15169 },
        { "asn_org", "Google LLC" },
        { "threat_intel", "clean" }
    }
};
```

## Testing

Comprehensive tests are available in `/sakin-utils/Sakin.Common.Tests/Validation/`:

- Validation tests for valid and invalid events
- Serialization/deserialization tests with data loss verification
- Schema conformance tests
- Edge case testing (boundary values, null handling)

Run tests with:

```bash
dotnet test /home/engine/project/sakin-utils/Sakin.Common.Tests
```

## Future Extensions

The schema is designed to be extended with additional event types:

- `DnsEvent` - DNS-specific properties
- `HttpEvent` - Detailed HTTP analysis
- `TlsEvent` - TLS certificate and cipher information
- `ProcessEvent` - Process execution details
- `FileEvent` - File system operations
- `AuthenticationEvent` - Authentication attempts and results

Each specialized event should:
1. Inherit from `NormalizedEvent`
2. Add type-specific properties
3. Update the JSON Schema definition
4. Add corresponding validation tests

## Best Practices

1. **Always validate events** before persisting to database or sending to message queue
2. **Use record types** for immutability and value-based equality
3. **Set appropriate EventType** when creating specialized events
4. **Include metadata** for additional context that doesn't fit standard properties
5. **Use UTC timestamps** consistently across all events
6. **Sanitize inputs** before creating events to prevent injection attacks
7. **Test serialization** round-trips to ensure no data loss

## References

- JSON Schema Specification: https://json-schema.org/
- System.Text.Json Documentation: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/
- C# Records: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record
- Json.Schema.Net: https://docs.json-everything.net/schema/basics/
