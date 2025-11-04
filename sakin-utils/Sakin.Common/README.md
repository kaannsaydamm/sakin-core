# Sakin.Common

Shared class library containing common models, utilities, configuration helpers, and abstractions used across the Sakin security platform.

## Overview

Sakin.Common provides reusable components to reduce code duplication and ensure consistency across all Sakin services. It includes normalized event models, database configuration, string utilities, TLS parsing, and logging extensions.

## Features

- **Normalized Event Models**: Standard event structures for security data
- **Configuration Helpers**: Database connection configuration and management
- **Redis Cache**: Simple Redis client wrapper for caching operations
- **String Utilities**: Safe string cleaning and sanitization
- **TLS Parser**: TLS ClientHello message parsing for SNI extraction
- **Logging Extensions**: Structured logging helpers

## Installation

Add a project reference to `Sakin.Common` in your `.csproj` file:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/Sakin.Common/Sakin.Common.csproj" />
</ItemGroup>
```

Or use the .NET CLI:

```bash
dotnet add reference path/to/Sakin.Common/Sakin.Common.csproj
```

## Components

### Models

#### Event Enums

- **EventType**: Categorizes security events (NetworkTraffic, DnsQuery, HttpRequest, TlsHandshake, etc.)
- **Severity**: Event severity levels (Info, Low, Medium, High, Critical)
- **Protocol**: Network protocols (TCP, UDP, HTTP, HTTPS, DNS, SSH, etc.)

#### NormalizedEvent

Base class for all security events with common properties:

```csharp
using Sakin.Common.Models;

var evt = new NormalizedEvent
{
    EventType = EventType.NetworkTraffic,
    Severity = Severity.Medium,
    SourceIp = "192.168.1.100",
    DestinationIp = "10.0.0.1",
    Protocol = Protocol.TCP,
    Metadata = new Dictionary<string, object>
    {
        { "bytes_sent", 1024 }
    }
};
```

#### NetworkEvent

Specialized event for network traffic with additional properties:

```csharp
var networkEvent = new NetworkEvent
{
    SourceIp = "192.168.1.100",
    DestinationIp = "10.0.0.1",
    BytesSent = 1024,
    BytesReceived = 2048,
    Sni = "example.com",
    HttpUrl = "https://example.com/api"
};
```

### Configuration

#### DatabaseOptions

Configuration class for PostgreSQL database connections:

```csharp
using Sakin.Common.Configuration;

// In appsettings.json:
{
  "Database": {
    "Host": "localhost",
    "Username": "postgres",
    "Password": "yourpassword",
    "Database": "network_db",
    "Port": 5432
  }
}

// In Program.cs:
services.Configure<DatabaseOptions>(
    configuration.GetSection(DatabaseOptions.SectionName));
```

### Database

#### DatabaseConnectionFactory

Factory for creating PostgreSQL database connections:

```csharp
using Sakin.Common.Database;
using Microsoft.Extensions.DependencyInjection;

// Register in DI container:
services.AddSingleton<IDatabaseConnectionFactory, DatabaseConnectionFactory>();

// Use in your service:
public class MyService
{
    private readonly IDatabaseConnectionFactory _connectionFactory;
    
    public MyService(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
    
    public void DoWork()
    {
        using var conn = _connectionFactory.CreateConnection();
        if (conn != null)
        {
            // Use the connection
        }
    }
}
```

### Cache

#### Redis Client

Simple Redis client wrapper for basic caching operations:

```csharp
using Sakin.Common.Cache;
using Sakin.Common.DependencyInjection;

// In appsettings.json:
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}

// In Program.cs:
services.AddRedisClient();
services.Configure<RedisOptions>(
    configuration.GetSection(RedisOptions.SectionName));

// Use in your service:
public class MyService
{
    private readonly IRedisClient _redis;
    
    public MyService(IRedisClient redis)
    {
        _redis = redis;
    }
    
    public async Task DoWork()
    {
        // Set a value
        await _redis.StringSetAsync("mykey", "myvalue", TimeSpan.FromMinutes(5));
        
        // Get a value
        string? value = await _redis.StringGetAsync("mykey");
        
        // Check if key exists
        bool exists = await _redis.KeyExistsAsync("mykey");
        
        // Increment counter
        long counter = await _redis.IncrementAsync("counter");
        
        // Delete key
        await _redis.KeyDeleteAsync("mykey");
    }
}
```

### Utilities

#### StringHelper

Safe string cleaning and sanitization:

```csharp
using Sakin.Common.Utilities;

// Remove non-printable characters
string cleaned = StringHelper.CleanString("Hello\x00World");
// Returns: "HelloWorld"

// Clean and truncate to max length
string sanitized = StringHelper.SanitizeInput("Some long text...", maxLength: 255);
```

#### TLSParser

Parse TLS ClientHello messages to extract SNI (Server Name Indication):

```csharp
using Sakin.Common.Utilities;

byte[] tlsPayload = GetTLSPayload();
var (success, sni) = TLSParser.ParseTLSClientHello(tlsPayload);

if (success)
{
    Console.WriteLine($"SNI: {sni}");
}
```

### Logging

#### LoggerExtensions

Structured logging extensions for common scenarios:

```csharp
using Sakin.Common.Logging;
using Microsoft.Extensions.Logging;

// Log packet capture
_logger.LogPacketCapture("192.168.1.1", "10.0.0.1", "TCP");

// Log SNI capture
_logger.LogSniCapture("example.com", "192.168.1.1", "10.0.0.1");

// Log database errors
_logger.LogDatabaseError(exception, "SavePacket");

// Log network interface detection
_logger.LogNetworkInterfaceDetected("eth0", "Ethernet Adapter");

// Log skipped interface
_logger.LogNetworkInterfaceSkipped("lo", "Loopback interface");
```

## Testing

The library includes comprehensive unit tests. Run them with:

```bash
cd Sakin.Common.Tests
dotnet test
```

## Dependencies

- **.NET 8.0**: Target framework
- **Npgsql**: PostgreSQL database driver
- **Microsoft.Extensions.Options**: Configuration binding
- **Microsoft.Extensions.Logging.Abstractions**: Logging interfaces

## Usage in Projects

### Network Sensor

The network sensor service uses Sakin.Common for:
- Database configuration and connection management
- String cleaning for captured SNI data
- TLS ClientHello parsing (planned)

### Future Services

Other Sakin services should use Sakin.Common for:
- Consistent event model structures
- Shared database configuration
- Common utility functions
- Standardized logging patterns

## Version

Current version: 1.0.0

## License

See the LICENSE file in the repository root.
