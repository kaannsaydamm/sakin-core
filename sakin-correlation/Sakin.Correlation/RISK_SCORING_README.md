# Risk Scoring Engine Implementation

## Overview

This document describes the implementation of the Risk Scoring Engine for Sakin, a sophisticated multi-factor risk assessment system that calculates 0-100 risk scores for security alerts to enable SOC analysts to prioritize the most critical threats.

## Architecture

### Asynchronous, Non-Blocking Design

The risk scoring engine is designed to never block the core correlation pipeline:

1. **Alert Creation**: `AlertCreatorService` creates alerts immediately with `Status="pending_score"`
2. **Internal Queuing**: Alerts are queued to `RiskScoringWorker` via `System.Threading.Channels.Channel<T>`
3. **Async Processing**: `RiskScoringWorker` processes alerts in the background, calculating and updating risk scores
4. **User Risk Tracking**: `UserRiskProfileWorker` processes normalized events to maintain user risk profiles

This ensures that correlation performance is never impacted by risk scoring calculations.

## Core Components

### 1. Risk Scoring Models

#### RiskScore Record
```csharp
public record RiskScore
{
    public int Score { get; init; } // 0-100
    public RiskLevel Level { get; init; } // Low, Medium, High, Critical
    public Dictionary<string, double> Factors { get; init; } // breakdown of scoring
    public string Reasoning { get; init; } // human-readable explanation
}
```

#### Configuration Models
- `RiskScoringConfiguration`: Main configuration section
- `RiskScoringFactorsConfiguration`: Individual factor weights and multipliers

### 2. Risk Factor Services

#### TimeOfDayService
- Determines if events occur outside business hours
- Configurable business hours (default: 09:00-17:00)
- Returns 1.2x multiplier for off-hours events

#### UserRiskProfileService
- Tracks 7-day rolling user risk scores in Redis
- Event-based risk increases:
  - `auth.failed`: +5 points
  - `auth.lockout`: +10 points
  - `auth.privilege_escalation`: +15 points
  - `malware.detected`: +25 points
  - `network.command_and_control`: +30 points
  - etc.

#### RiskScoringService
- Main scoring engine combining all factors
- Supports both sync and async operation
- Generates human-readable reasoning

### 3. Background Workers

#### RiskScoringWorker
- Processes alerts via in-memory `Channel<T>` queue
- Calculates risk scores using `RiskScoringService`
- Updates alerts in database with `Status="scored"`

#### UserRiskProfileWorker
- Processes normalized events for user risk profiling
- Updates Redis with rolling 7-day risk scores
- Uses Redis list for event queuing

## Risk Scoring Formula

The configurable formula combines multiple risk factors:

```csharp
final_score = (base_score + asset_boost + threat_intel_boost + user_risk_boost + anomaly_boost) * time_multiplier
```

### Risk Factors

| Factor | Range | Description |
|---------|--------|-------------|
| **Base Severity** | 20-100 | Low=20, Medium=50, High=75, Critical=100 |
| **Asset Criticality** | 1.0x-2.0x | Low=1.0x, Medium=1.2x, High=1.5x, Critical=2.0x |
| **Threat Intel** | 0-30 points | Based on known malicious IP scores |
| **Time of Day** | 1.0x-1.2x | 1.2x multiplier for off-hours events |
| **User Risk Profile** | 0-50 points | Based on 7-day user behavior history |
| **Anomaly Score** | 0-20 points | ML-detected anomalies (future enhancement) |

Score is clamped to 0-100 range.

## Database Schema

### Alert Table Updates

```sql
ALTER TABLE alerts ADD COLUMN 
    risk_score INT NOT NULL DEFAULT 0,
    risk_level TEXT NOT NULL DEFAULT 'low',
    risk_factors JSONB,
    reasoning TEXT;

-- Indexes for efficient sorting
CREATE INDEX ix_alerts_risk_score ON alerts(risk_score);
CREATE INDEX ix_alerts_risk_level ON alerts(risk_level);
CREATE INDEX ix_alerts_risk_score_triggered_at ON alerts(risk_score, triggered_at);
```

## Configuration

### appsettings.json

```json
{
  "RiskScoring": {
    "Enabled": true,
    "Factors": {
      "BaseWeights": {
        "Low": 20,
        "Medium": 50,
        "High": 75,
        "Critical": 100
      },
      "AssetMultipliers": {
        "Low": 1.0,
        "Medium": 1.2,
        "High": 1.5,
        "Critical": 2.0
      },
      "OffHoursMultiplier": 1.2,
      "ThreatIntelMaxBoost": 30,
      "AnomalyMaxBoost": 20
    },
    "BusinessHours": "09:00-17:00"
  }
}
```

## Integration Points

### Existing Enrichment
- **GeoIP Data**: Used for asset context and location-based analysis
- **Threat Intel**: Integrated for malicious IP detection
- **Asset Data**: Asset criticality from AssetCacheService

### Alert Pipeline
- **Backward Compatible**: Existing `CreateAlertAsync` method unchanged
- **Enhanced**: New `CreateAlertWithRiskScoringAsync` for risk scoring
- **Status Flow**: `new` → `pending_score` → `scored`

## Testing

### Test Coverage

1. **RiskScoringServiceTests.cs**
   - Base severity scoring
   - Asset criticality multipliers
   - Threat intel integration
   - Time-of-day factors
   - User risk profiles
   - Anomaly scores
   - Multi-factor combinations
   - Score capping at 100

2. **TimeOfDayServiceTests.cs**
   - Business hours parsing
   - Time-based logic validation
   - Edge cases (invalid formats)

3. **UserRiskProfileServiceTests.cs**
   - User risk calculation
   - Redis interactions
   - Event type risk scoring
   - Score capping and TTL

### Running Tests

```bash
# Build the project
dotnet build sakin-correlation/Sakin.Correlation/Sakin.Correlation.csproj

# Run risk scoring tests
dotnet test tests/Sakin.Correlation.Tests/Sakin.Correlation.Tests.csproj --filter FullyQualifiedName~RiskScoringServiceTests

# Run all correlation tests
dotnet test tests/Sakin.Correlation.Tests/Sakin.Correlation.Tests.csproj
```

## Performance Considerations

### Non-Blocking Design
- Alert creation never waits for risk scoring
- Uses in-memory `Channel<T>` for efficient queuing
- Background workers process independently

### Database Optimization
- Risk score indexes for efficient sorting
- JSONB storage for factor breakdowns
- Proper query optimization for dashboard

### Caching Strategy
- User risk profiles cached in Redis (7-day TTL)
- Risk factors stored in database for historical analysis
- Configurable TTLs per data type

## Monitoring and Observability

### Logging
- Detailed logging for scoring decisions
- Factor breakdown logging at debug level
- Error handling with proper correlation IDs

### Metrics
- Risk score distribution histograms
- Processing latency metrics
- Queue depth monitoring
- Error rate tracking

## Future Enhancements

### Machine Learning Integration
- Anomaly detection models
- Behavioral risk scoring
- Predictive risk assessment

### Advanced Features
- Temporal risk patterns
- Peer group analysis
- Risk trend analysis
- Automated threshold tuning

## Deployment

### Prerequisites
- PostgreSQL database with migration applied
- Redis for user risk profiles
- Configured business hours and risk weights

### Migration Steps
1. Apply database migration: `20241106120000_AddRiskScoringToAlerts`
2. Update configuration with risk scoring settings
3. Deploy updated correlation service
4. Monitor risk scoring queue processing
5. Validate risk score calculations

### Rollback Strategy
- Disable risk scoring via configuration (`"Enabled": false`)
- Alerts will be created without risk scores
- No impact on core correlation functionality

## Troubleshooting

### Common Issues

1. **Risk Scores Not Appearing**
   - Check if `RiskScoring.Enabled` is true
   - Verify `RiskScoringWorker` is running
   - Check Redis connectivity for user risk profiles

2. **Incorrect Risk Calculations**
   - Validate configuration weights
   - Check asset criticality data
   - Verify threat intel enrichment

3. **Performance Issues**
   - Monitor queue depth in RiskScoringWorker
   - Check database query performance
   - Verify Redis connection health

### Debug Commands

```bash
# Check risk scoring queue status
redis-cli llen sakin:user_risk:event_queue

# Examine user risk profile
redis-cli get "sakin:user_risk:username"

# Check recent risk scores in database
SELECT id, risk_score, risk_level, reasoning 
FROM alerts 
WHERE risk_score > 0 
ORDER BY triggered_at DESC 
LIMIT 10;
```

## Conclusion

The Risk Scoring Engine provides a sophisticated, configurable, and performant solution for prioritizing security alerts. Its asynchronous architecture ensures no impact on core correlation performance while providing rich risk context for SOC analysts.

The modular design allows for easy extension and tuning of risk factors, making it adaptable to different organizational requirements and threat landscapes.