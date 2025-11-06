# Redis State Management & Aggregation

This document describes the Redis-based state management and aggregation functionality added to the Sakin Correlation Engine.

## Overview

The correlation engine now supports stateful aggregation rules that can count events within time windows, group by specific fields, and trigger alerts when thresholds are reached.

## Architecture

### Components

1. **RedisStateManager** - Manages counters in Redis with atomic operations
2. **AggregationEvaluatorService** - Evaluates aggregation conditions against incoming events
3. **RuleEvaluatorV2** - Extended rule evaluator supporting both legacy and V2 rule formats
4. **RuleLoaderServiceV2** - Loads both legacy and V2 rule formats
5. **RedisCleanupService** - Background service for cleaning up expired Redis keys

### Rule Format V2

The new rule format supports inline aggregation conditions:

```json
{
  "id": "rule-bruteforce-01",
  "name": "RDP Brute Force",
  "enabled": true,
  "trigger": {
    "source_types": ["windows-eventlog"],
    "match": {"event_code":"4625"}
  },
  "condition": {
    "aggregation": {
      "function": "count",
      "field": "Normalized.username",
      "group_by": "Normalized.source_ip",
      "window_seconds": 300
    },
    "operator": "gte",
    "value": 10
  },
  "severity": "high"
}
```

### Configuration

Add to `appsettings.json`:

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "KeyPrefix": "sakin:correlation:",
    "DefaultTTL": 3600
  },
  "Aggregation": {
    "MaxWindowSize": 86400,
    "CleanupInterval": 300
  }
}
```

## Key Features

### Sliding Time Windows

- Uses Unix timestamp division for consistent window boundaries
- Window ID calculated as: `timestamp / window_seconds`
- Automatic cleanup via Redis TTL

### Group-By Support

- Groups events by field values (e.g., source IP, username)
- Separate counters maintained for each group
- Supports dot notation for nested fields (`Normalized.source_ip`)

### Atomic Operations

- All Redis operations are atomic and thread-safe
- Counter increments are atomic
- TTL set only on first increment to avoid race conditions

### Redis Key Format

```
{prefix}rule:{ruleId}:group:{groupValue}:window:{windowId}
```

Example:
```
sakin:correlation:rule:bruteforce-01:group:192.168.1.100:window:12345678 = 5
```

## Usage Examples

### Brute Force Detection

Detects 10 failed login attempts from the same IP within 5 minutes:

```json
{
  "id": "rdp-bruteforce",
  "name": "RDP Brute Force Detection",
  "trigger": {
    "source_types": ["windows-eventlog"],
    "match": {"event_code":"4625"}
  },
  "condition": {
    "aggregation": {
      "function": "count",
      "group_by": "Normalized.source_ip",
      "window_seconds": 300
    },
    "operator": "gte",
    "value": 10
  }
}
```

### Rate Limiting

Detects more than 100 requests from the same IP in 1 minute:

```json
{
  "id": "rate-limit",
  "name": "Request Rate Limiting",
  "trigger": {
    "source_types": ["web-access"],
    "match": {"status_code":"200"}
  },
  "condition": {
    "aggregation": {
      "function": "count",
      "group_by": "Normalized.sourceIp",
      "window_seconds": 60
    },
    "operator": "gt",
    "value": 100
  }
}
```

## Backward Compatibility

- Legacy rule format continues to work unchanged
- Both formats evaluated simultaneously
- V2 rules are processed by the new aggregation evaluator
- Legacy rules continue to use the original rule evaluator

## Performance Considerations

### Redis Performance

- All operations are O(1) complexity
- Minimal memory overhead per counter
- Automatic cleanup prevents memory leaks

### Window Size Limits

- Maximum window size: 86400 seconds (24 hours)
- Minimum practical window: 1 second
- Larger windows use less Redis memory

### Cleanup Strategy

- Redis TTL automatically expires old keys
- Background cleanup service handles edge cases
- Configurable cleanup interval (default: 5 minutes)

## Testing

Run the aggregation test:

```bash
dotnet run --project . AggregationTest.RunTest
```

Or create test events and observe Redis counters:

```bash
# Check Redis counters
redis-cli
> KEYS sakin:correlation:*
> GET sakin:correlation:rule:test-bruteforce-01:group:192.168.1.100:window:12345678
```

## Troubleshooting

### Common Issues

1. **Redis Connection**: Ensure Redis is running and accessible
2. **Key Prefix Conflicts**: Use unique prefixes for different environments
3. **Window Size**: Very small windows may miss events due to timing
4. **Memory Usage**: Monitor Redis memory with many active windows

### Debug Logging

Enable debug logging to see detailed aggregation evaluation:

```json
{
  "Logging": {
    "LogLevel": {
      "Sakin.Correlation.Services.AggregationEvaluatorService": "Debug",
      "Sakin.Correlation.Services.RedisStateManager": "Debug"
    }
  }
}
```

## Future Enhancements

### Planned Features

1. **Additional Aggregation Functions**: Sum, Average, Min, Max
2. **Multiple Group-By Fields**: Composite grouping
3. **Custom Field Extractors**: Support for complex field extraction
4. **Redis Clustering**: Support for Redis cluster deployments
5. **Metrics Dashboard**: Real-time aggregation statistics

### Extensibility

The architecture is designed to support:

- Custom aggregation functions via `IAggregationEvaluator`
- Alternative state stores via `IRedisStateManager`
- Custom rule formats via `IRuleLoaderService`
- Additional cleanup strategies via `IHostedService`