# S.A.K.I.N. Anomaly Detection Engine

## Overview

The S.A.K.IN. Anomaly Detection Engine implements ML-based behavioral analysis using a decoupled architecture that separates analytical workloads from real-time ingestion. This design ensures high throughput in the event processing pipeline while providing sophisticated anomaly detection capabilities.

## Architecture

### Components

1. **Sakin.Analytics.ClickHouseSink** - Event Persistence Service
   - Consumes `normalized-events` from Kafka
   - Batch-writes events to ClickHouse (1000 events or 5 seconds)
   - Uses System.Threading.Channels for efficient buffering
   - Implements Polly retry policies for resilience

2. **Sakin.Analytics.BaselineWorker** - Baseline Calculation Service
   - Runs on a timer (hourly)
   - Analyzes 7 days of historical data in ClickHouse
   - Calculates statistical baselines (mean, stddev) for:
     - User hourly activity patterns
     - User-host connection counts
     - Unique destination ports per user-host
   - Stores baselines in Redis with 25-hour TTL

3. **AnomalyDetectionService** (in sakin-correlation)
   - Real-time anomaly scoring during alert creation
   - Reads baselines from Redis (with memory cache)
   - Calculates Z-scores for incoming events
   - Integrates with RiskScoringService

4. **RiskScoringService** (enhanced)
   - Calls AnomalyDetectionService for each alert
   - Applies anomaly boost (0-20 points) to risk score
   - Includes anomaly reasoning in alert details

## Data Flow

```
[Kafka: normalized-events] 
    ↓
[ClickHouseSink] → [ClickHouse: events table]
    ↓ (hourly)
[BaselineWorker] → [ClickHouse query] → [Redis: baselines]
    ↓ (on alert creation)
[RiskScoringService] → [AnomalyDetectionService] → [Redis: read baseline] → [Z-score calculation]
```

## Baseline Metrics

### 1. User Hourly Activity
- **Key**: `sakin:baseline:user_hour:{username}:{hour_of_day}`
- **Measures**: Event count per hour (0-23) for each user
- **Purpose**: Detect logins/activity at unusual hours
- **Example**: User logs in at 3 AM when baseline shows 9 AM activity

### 2. User-Host Connection Count
- **Key**: `sakin:baseline:user_host_conn:{username}:{hostname}`
- **Measures**: Connection count from specific user-host pairs
- **Purpose**: Detect unusual connection volumes
- **Example**: Admin user makes 500 connections from workstation (normal: 50)

### 3. Unique Destination Ports
- **Key**: `sakin:baseline:user_host_ports:{username}:{hostname}`
- **Measures**: Number of unique ports accessed per user-host
- **Purpose**: Detect port scanning behavior
- **Example**: User accesses 200 unique ports (normal: 10)

## Z-Score Calculation

The system uses statistical Z-scores to quantify deviations from baseline:

```
Z-Score = (current_value - mean) / stddev
```

- **Z < 2.5**: Normal (Score: 0)
- **2.5 ≤ Z < 5.0**: Anomalous (Score: 0-100 linear)
- **Z ≥ 5.0**: Highly anomalous (Score: 100)

## Configuration

### BaselineAggregationOptions (Sakin.Common)

```json
{
  "BaselineAggregation": {
    "Enabled": true,
    "KafkaTopic": "normalized-events",
    "KafkaConsumerGroup": "clickhouse-sink",
    "BatchSize": 1000,
    "BatchTimeoutSeconds": 5,
    "BaselineTtlHours": 25,
    "AnalysisWindowDays": 7,
    "ClickHouseConnectionString": "Host=clickhouse;Port=9000;Database=sakin;User=default;Password="
  }
}
```

### AnomalyDetectionOptions (sakin-correlation)

```json
{
  "AnomalyDetection": {
    "Enabled": true,
    "ZScoreThreshold": 2.5,
    "CacheDurationSeconds": 60,
    "AnomalyMaxBoost": 20.0,
    "RedisKeyPrefix": "sakin:baseline"
  }
}
```

## ClickHouse Schema

### Events Table

```sql
CREATE TABLE events (
    event_id UUID,
    received_at DateTime64(3),
    event_timestamp DateTime64(3),
    event_type LowCardinality(String),
    severity LowCardinality(String),
    source_ip String,
    destination_ip String,
    source_port UInt16,
    destination_port UInt16,
    protocol LowCardinality(String),
    username String,
    hostname String,
    device_name String,
    source String,
    source_type LowCardinality(String)
) ENGINE = MergeTree()
PARTITION BY toYYYYMM(toDate(event_timestamp))
ORDER BY (toDate(event_timestamp), username, hostname, event_timestamp)
TTL toDate(event_timestamp) + INTERVAL 30 DAY;
```

### Baseline Features Table (Optional - Redis is primary)

```sql
CREATE TABLE baseline_features (
    calculated_at DateTime64(3),
    metric_type LowCardinality(String),
    username String,
    hostname String,
    hour_of_day UInt8,
    mean Float64,
    stddev Float64,
    count UInt64,
    min_value Float64,
    max_value Float64
) ENGINE = ReplacingMergeTree(calculated_at)
ORDER BY (metric_type, username, hostname, hour_of_day);
```

## Sample Queries

### Calculate Hourly Activity Baseline

```sql
SELECT 
    username,
    toHour(event_timestamp) as hour_of_day,
    avg(event_count) as mean,
    stddevPop(event_count) as stddev,
    count() as sample_count
FROM (
    SELECT 
        username,
        toStartOfHour(event_timestamp) as event_timestamp,
        count() as event_count
    FROM events
    WHERE event_timestamp >= now() - INTERVAL 7 DAY
      AND username != ''
    GROUP BY username, event_timestamp
)
GROUP BY username, hour_of_day
HAVING sample_count >= 3;
```

### Calculate Connection Count Baseline

```sql
SELECT 
    username,
    hostname,
    avg(conn_count) as mean,
    stddevPop(conn_count) as stddev
FROM (
    SELECT 
        username,
        hostname,
        count() as conn_count
    FROM events
    WHERE event_timestamp >= now() - INTERVAL 7 DAY
      AND username != ''
      AND hostname != ''
    GROUP BY username, hostname, toStartOfHour(event_timestamp)
)
GROUP BY username, hostname
HAVING count() >= 3;
```

## API Integration

### RiskScoringService Enhancement

The RiskScoringService automatically calls AnomalyDetectionService:

```csharp
var anomalyResult = await _anomalyDetectionService.CalculateAnomalyScoreAsync(envelope.Normalized);
anomalyBoost = anomalyResult.Score / 100.0 * _config.Factors.AnomalyMaxBoost;
```

### Example Anomaly Result

```json
{
  "score": 85.0,
  "is_anomalous": true,
  "z_score": 4.2,
  "reasoning": "Activity at hour 3 is 4.20 standard deviations from normal (mean: 9.50, current: 1.00)",
  "baseline_mean": 9.5,
  "baseline_stddev": 1.2,
  "current_value": 1.0,
  "metric_name": "hourly_activity"
}
```

## Performance Considerations

### Caching Strategy

1. **Redis Cache** (primary)
   - Baselines stored with 25-hour TTL
   - Single Redis GET per anomaly check
   - Key pattern: `sakin:baseline:{metric}:{identifiers}`

2. **Memory Cache** (secondary)
   - 60-second TTL (configurable)
   - Prevents repeated calculations for same event
   - Key pattern: `anomaly:{username}:{hostname}:{timestamp_hour}`

### Batch Processing

- **ClickHouseSink**: Buffers up to 1000 events or 5 seconds
- **BaselineWorker**: Processes all metrics in single ClickHouse connection
- **Polly Retry**: 3 retries with exponential backoff

## Monitoring

### Key Metrics

- Events written to ClickHouse (via ClickHouseSink logs)
- Baseline calculation duration (via BaselineWorker logs)
- Anomaly detection hits/misses (via AnomalyDetectionService logs)
- Redis cache hit ratio

### Sample Log Output

```
[ClickHouseSink] Successfully inserted 1000 events into ClickHouse
[BaselineWorker] Stored 250 baseline snapshots for user_hour
[AnomalyDetectionService] Returning cached anomaly score for admin
[RiskScoringService] Calculated risk score 85 (High) for alert 123 with anomaly boost 17.0
```

## Testing

### Unit Tests

Located in:
- `tests/Sakin.Analytics.Tests` (to be created)
- `tests/Sakin.Correlation.Tests` (existing, enhanced)

### Integration Tests

Use Testcontainers to spin up:
- Kafka (for event streaming)
- Redis (for baseline storage)
- ClickHouse (for event storage and queries)

### Manual Testing

1. Publish normal events (9 AM, user: alice)
2. Wait for baseline calculation (1 hour)
3. Publish anomalous event (3 AM, user: alice)
4. Verify anomaly score > 0 in risk scoring logs

## Troubleshooting

### Baselines Not Found

- **Check**: BaselineWorker logs for calculation errors
- **Check**: Redis keys using `redis-cli KEYS "sakin:baseline:*"`
- **Fix**: Ensure ClickHouse has sufficient event history (7 days)

### Low/No Anomaly Scores

- **Check**: Z-score threshold (default: 2.5)
- **Check**: Standard deviation in baseline (stddev = 0 → no anomaly)
- **Fix**: Adjust `ZScoreThreshold` in configuration

### ClickHouse Connection Errors

- **Check**: Connection string in appsettings
- **Check**: ClickHouse service availability
- **Fix**: Verify network connectivity and credentials

## Future Enhancements

1. **Adaptive Thresholds**: Per-user Z-score thresholds
2. **Additional Metrics**: Failed login attempts, data volume, protocol distribution
3. **Machine Learning**: Replace Z-score with LSTM/Isolation Forest models
4. **Real-time Alerts**: Emit Kafka events for anomalies detected
5. **Dashboard**: Visualize baselines and anomalies in Grafana/Kibana
