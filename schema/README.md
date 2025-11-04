# SAKIN Event Schema

This directory contains the JSON Schema definitions for normalized events in the SAKIN platform.

## Files

- **event-schema.json**: JSON Schema definition for all event types (Draft 2020-12)
- **sample-events.json**: Sample events demonstrating the schema usage

## Schema Validation

The JSON schema can be used to validate event data in any language that supports JSON Schema validation.

### Online Validation

You can validate events using online tools:
- [JSON Schema Validator](https://www.jsonschemavalidator.net/)
- Upload `event-schema.json` as the schema
- Paste event JSON from `sample-events.json` to test

### C# Validation

Use the `EventValidator` class from `Sakin.Common.Validation`:

```csharp
using Sakin.Common.Validation;
using Sakin.Common.Models;

// Load validator
var validator = EventValidator.FromFile("schema/event-schema.json");

// Validate an event object
var evt = new NetworkEvent
{
    SourceIp = "192.168.1.100",
    DestinationIp = "8.8.8.8",
    Protocol = Protocol.HTTPS,
    BytesSent = 1024
};

var result = validator.Validate(evt);
if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine(error);
    }
}
```

### Python Validation

```python
import json
import jsonschema

# Load schema
with open('schema/event-schema.json') as f:
    schema = json.load(f)

# Load and validate event
with open('event.json') as f:
    event = json.load(f)

try:
    jsonschema.validate(event, schema)
    print("Valid!")
except jsonschema.ValidationError as e:
    print(f"Invalid: {e.message}")
```

### JavaScript/TypeScript Validation

```typescript
import Ajv from 'ajv';
import schema from './schema/event-schema.json';

const ajv = new Ajv();
const validate = ajv.compile(schema);

const event = {
  id: "123e4567-e89b-12d3-a456-426614174000",
  timestamp: "2024-11-04T10:30:00Z",
  eventType: "networkTraffic",
  severity: "info",
  sourceIp: "192.168.1.100",
  destinationIp: "8.8.8.8",
  protocol: "https",
  metadata: {}
};

if (validate(event)) {
  console.log("Valid!");
} else {
  console.log("Errors:", validate.errors);
}
```

## Event Types

### NormalizedEvent (Base Type)

The base event type with core security event properties:
- Unique ID and timestamp
- Event type and severity
- Source and destination IP addresses
- Network protocol
- Optional ports, payload, and metadata
- Device/sensor information

### NetworkEvent (Extends NormalizedEvent)

Specialized event type for network traffic monitoring:
- Bytes sent/received
- Packet count
- TLS SNI (Server Name Indication)
- HTTP URL, method, status code
- User-Agent string

## Enum Values

All enum values use **camelCase** formatting in JSON:

### EventType
- `unknown`, `networkTraffic`, `dnsQuery`, `httpRequest`, `tlsHandshake`
- `sshConnection`, `fileAccess`, `processExecution`, `authenticationAttempt`
- `systemLog`, `securityAlert`

### Severity
- `unknown`, `info`, `low`, `medium`, `high`, `critical`

### Protocol
- `unknown`, `tcp`, `udp`, `icmp`, `http`, `https`, `dns`, `ssh`, `ftp`, `smtp`, `tls`

## Schema Version

- **JSON Schema Version**: Draft 2020-12
- **Schema URI**: https://sakin.io/schemas/event-schema.json

## Testing

To test the schema and samples:

```bash
# Run C# validation tests
dotnet test sakin-utils/Sakin.Common.Tests

# Validate sample events using jq and a JSON Schema validator
for sample in $(jq -r '.sampleEvents[].description' schema/sample-events.json); do
  echo "Validating: $sample"
  # Extract and validate each sample
done
```

## Future Extensions

The schema is designed to support additional event types:
- `DnsEvent` - DNS query details
- `HttpEvent` - HTTP transaction analysis  
- `TlsEvent` - TLS handshake and certificate info
- `ProcessEvent` - Process execution monitoring
- `FileEvent` - File system operations
- `AuthenticationEvent` - Authentication tracking

Each new type should extend NormalizedEvent and add type-specific properties.

## References

- [JSON Schema Documentation](https://json-schema.org/)
- [Understanding JSON Schema](https://json-schema.org/understanding-json-schema/)
- [SAKIN Event Schema Documentation](../docs/event-schema.md)
