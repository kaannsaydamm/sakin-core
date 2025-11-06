# S.A.K.I.N. Ingest Service

The ingest service is responsible for consuming raw events from Kafka, normalizing them, and enriching them with additional metadata before publishing to downstream topics.

## Features

### Event Normalization
- Supports multiple log formats: Windows EventLog, Syslog, Apache Access Logs, Fortinet
- Extracts common fields: source/destination IPs, ports, protocols, timestamps
- Handles parsing errors gracefully with passthrough normalization

### GeoIP Enrichment (Sprint 6)
Enriches events with geographical data based on source and destination IP addresses.

#### Configuration
```json
{
  "GeoIp": {
    "Enabled": true,
    "DatabasePath": "/data/GeoLite2-City.mmdb",
    "CacheTtlSeconds": 3600,
    "CacheMaxSize": 10000
  }
}
```

#### Features
- **Private IP Detection**: Automatically identifies private networks (10.x, 172.16-31.x, 192.168.x, 169.254.x, loopback)
- **Caching**: In-memory caching with configurable TTL and size limits to improve performance
- **Graceful Degradation**: Service continues to operate when GeoIP database is unavailable
- **Public IP Resolution**: Uses MaxMind GeoLite2 database for public IP lookups

#### Enrichment Output
GeoIP data is added to the `EventEnvelope.Enrichment` field:

```json
{
  "Enrichment": {
    "source_geo": {
      "country": "United States",
      "countryCode": "US", 
      "city": "New York",
      "lat": 40.7128,
      "lon": -74.0060,
      "timezone": "America/New_York",
      "isPrivate": false
    },
    "dest_geo": {
      "country": "Private",
      "countryCode": "PR",
      "city": "Private Network", 
      "isPrivate": true
    }
  }
}
```

#### Database Setup
1. Download GeoLite2-City.mmdb from [MaxMind](https://dev.maxmind.com/geoip/geolite2-open-data-locations/)
2. Place in `/data/geoip/GeoLite2-City.mmdb` (mounted in Docker container)
3. Restart the ingest service

## Architecture

### Pipeline Flow
1. **Consume**: Raw events from Kafka `raw-events` topic
2. **Parse**: Using appropriate parser based on `sourceType`
3. **Enrich**: Add GeoIP data for source/destination IPs
4. **Publish**: Normalized events to Kafka `normalized-events` topic

### Components
- **EventIngestWorker**: Main worker service orchestrating the pipeline
- **ParserRegistry**: Registry of available log parsers
- **GeoIpService**: High-performance GeoIP lookup with caching
- **Message Serialization**: JSON-based event envelope handling

## Deployment

### Docker Compose
The service is configured in `deployments/docker-compose.dev.yml` with:
- GeoIP database volume mount (`../data/geoip:/data:ro`)
- Kafka, OpenSearch, ClickHouse dependencies
- Environment-based configuration

### Environment Variables
- `Kafka__BootstrapServers`: Kafka connection string
- `Kafka__RawEventsTopic`: Input topic for raw events
- `Kafka__NormalizedEventsTopic`: Output topic for normalized events
- `ASPNETCORE_ENVIRONMENT`: Runtime environment

## Development

### Building
```bash
dotnet build sakin-ingest/Sakin.Ingest/Sakin.Ingest.csproj
```

### Testing
```bash
dotnet test tests/Sakin.Ingest.Tests/Sakin.Ingest.Tests.csproj
```

### Adding New Parsers
1. Implement `IParser` interface
2. Register in `Program.cs` ParserRegistry
3. Add tests in `tests/Sakin.Ingest.Tests/Parsers/`