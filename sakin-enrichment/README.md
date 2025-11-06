# Sakin Threat Intelligence Service

Asynchronous threat intelligence enrichment service for S.A.K.I.N. Processes IoC (Indicators of Compromise) lookups from a Kafka queue and enriches them with threat scores from multiple providers (OTX, AbuseIPDB).

## Architecture

### Design Principles

- **Non-blocking Pipeline**: The main ingestion service (sakin-ingest) does NOT make blocking API calls to threat intel providers
- **Asynchronous Enrichment**: Cache misses are enqueued to `ti-lookup-queue` topic for background processing
- **Multi-provider Aggregation**: Combines scores from multiple threat intel sources (OTX, AbuseIPDB, MISP)
- **Rate Limiting**: Redis-based token bucket to respect provider quotas
- **Resilience**: Polly policies (retry, circuit breaker) handle provider failures gracefully

### Data Flow

```
EventIngestWorker
  ├─ Extract IoCs (IPs, domains, hashes)
  ├─ Query Redis cache
  ├─ Enrich event with cached results
  └─ Enqueue cache misses → ti-lookup-queue

ThreatIntelWorker
  ├─ Consume from ti-lookup-queue
  ├─ Check rate limits (Redis token bucket)
  ├─ Query providers (OTX, AbuseIPDB)
  ├─ Aggregate scores
  ├─ Cache results in Redis (7d/1h/24h TTL)
  └─ Commit offset
```

## Configuration

### appsettings.json

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "ThreatIntelConsumerGroup": "threat-intel-worker"
  },
  "ThreatIntel": {
    "Enabled": true,
    "Providers": [
      {
        "Type": "OTX",
        "Enabled": true,
        "ApiKey": "YOUR_OTX_KEY",
        "BaseUrl": "https://otx.alienvault.com/api/v1",
        "DailyQuota": 600
      },
      {
        "Type": "AbuseIPDB",
        "Enabled": true,
        "ApiKey": "YOUR_ABUSEIPDB_KEY",
        "BaseUrl": "https://api.abuseipdb.com/api/v2",
        "DailyQuota": 1000
      }
    ],
    "MaliciousScoreThreshold": 80,
    "MaliciousCacheTtlDays": 7,
    "CleanCacheTtlHours": 1,
    "NotFoundCacheTtlHours": 24,
    "LookupTopic": "ti-lookup-queue"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

## Features

### Threat Intel Providers

#### OTX (AlienVault Open Threat Exchange)
- Supports: IPv4, IPv6, domains, URLs, file hashes (MD5, SHA1, SHA256)
- Rate limit: 600 requests/day (free tier)
- Endpoint: `https://otx.alienvault.com/api/v1`

#### AbuseIPDB
- Supports: IPv4, IPv6 only
- Rate limit: 1000 reports/day (free tier)
- Endpoint: `https://api.abuseipdb.com/api/v2`

### Caching Strategy

| Score | TTL | Rationale |
|-------|-----|-----------|
| Malicious (≥80) | 7 days | Stable threat intel |
| Clean (<80) | 1 hour | Can change quickly |
| Not Found | 24 hours | Avoid repeated misses |

### Resilience

- **Retry Policy**: Exponential backoff (3 retries)
- **Circuit Breaker**: Opens after 3 failures, remains open for 5 minutes
- **Rate Limiting**: Token bucket via Redis (respects daily quotas)
- **Graceful Degradation**: Continues with other providers if one fails

## Building

```bash
dotnet build sakin-enrichment/Sakin.ThreatIntelService/Sakin.ThreatIntelService.csproj
```

## Running

```bash
dotnet run --project sakin-enrichment/Sakin.ThreatIntelService/Sakin.ThreatIntelService.csproj
```

## Testing

```bash
dotnet test tests/Sakin.Ingest.Tests/Sakin.Ingest.Tests.csproj --filter FullyQualifiedName~ThreatIntelServiceTests
```

## Integration with sakin-ingest

The ingest worker automatically:

1. Extracts IoCs from normalized events
2. Checks Redis cache for existing scores
3. Enriches event with cached results under `enrichment.threat_intel`
4. Enqueues cache misses to `ti-lookup-queue`

Example enrichment output:

```json
{
  "enrichment": {
    "threat_intel": {
      "source_ip": {
        "is_malicious": true,
        "score": 95,
        "feeds": ["OTX", "AbuseIPDB"],
        "last_seen": "2024-01-15T10:30:00Z",
        "details": {
          "status": "malicious"
        }
      }
    }
  }
}
```

## Integration with sakin-correlation

The correlation engine queries Redis directly during rule evaluation:

```csharp
var threatScore = await redisClient.StringGetAsync($"threatintel:ipv4:{ip}");
```

This ensures rules always have the latest threat intel data.

## Troubleshooting

### Rate Limit Exceeded

If you see warnings about rate limits:
- Adjust `DailyQuota` in config
- Consider enabling fewer providers
- Check token bucket key in Redis: `threatintel:ratelimit:{provider}:{yyyyMMdd}`

### Provider Failures

Check logs for:
- `CircuitBreakerOpenException`: Provider circuit breaker is open (will retry in 5 min)
- `HttpRequestException`: Network or auth issues (check API keys)
- `JsonException`: Unexpected response format

### Cache Issues

Verify Redis connection:
```bash
redis-cli -h localhost ping  # Should return PONG
redis-cli -h localhost KEYS "threatintel:*"
```

## Security

- API keys stored in `appsettings.json` (consider using secrets manager in production)
- No raw logs sent to external feeds, only anonymized IoCs
- Rate limiting prevents abuse
