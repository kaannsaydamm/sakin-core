# Alert Lifecycle Management

## Overview

The Alert Lifecycle Management system in Sakin provides comprehensive tracking and management of alert states throughout their lifetime. It includes deduplication to reduce alert noise, a state machine to enforce valid transitions, and support for manual and automatic resolution workflows.

## Architecture

### Components

1. **AlertDeduplicationService**: Redis-backed service that prevents duplicate alerts
2. **AlertLifecycleService**: Manages state transitions and enforces business rules
3. **AlertRepository**: Persists alert data with lifecycle tracking
4. **Panel API**: Exposes RESTful endpoints for lifecycle operations
5. **Auto-Resolution Service**: Automatically resolves stale alerts (to be implemented)

### Database Schema

Extended `alerts` table includes:
- `alert_count`: Number of times this alert has been triggered (deduplicated)
- `first_seen`: Timestamp of first occurrence
- `last_seen`: Timestamp of most recent occurrence
- `status_history`: JSONB array tracking all status transitions
- `dedup_key`: SHA256 hash for deduplication (rule_id + source_ip + dest_ip)
- Timestamp fields: `acknowledged_at`, `investigation_started_at`, `resolved_at`, `closed_at`, `false_positive_at`
- `resolution_comment`, `resolution_reason`: Lifecycle metadata

## Alert States

```
        ┌─→ Acknowledged ─→ UnderInvestigation ─┐
        │                                       │
New ────┼─→ PendingScore                        ├─→ Resolved ─→ Closed
        │                                       │
        └─→ FalsePositive ────────────────────→─┘
```

### State Definitions

- **New**: Initial state, alert just created
- **PendingScore**: Awaiting risk score calculation
- **Acknowledged**: SOC analyst has reviewed and acknowledged the alert
- **UnderInvestigation**: Active investigation ongoing
- **Resolved**: Incident has been resolved (issue remediated)
- **Closed**: Alert is closed and archived
- **FalsePositive**: Alert was determined to be a false positive

## Deduplication

### How It Works

1. On rule trigger, a dedup key is generated: `SHA256(rule_id:source_ip:dest_ip)`
2. Key is checked in Redis (TTL configured via `AlertLifecycle:DedupTtlMinutes`)
3. **Cache Hit**: Alert count incremented, LastSeen updated
4. **Cache Miss**: New alert created, key stored in Redis with TTL

### Configuration

```json
{
  "AlertLifecycle": {
    "DedupTtlMinutes": 60,
    "StaleAlertThresholdHours": 24
  }
}
```

### Redis Keys

Format: `dedup:SHA256_HASH`

Example: `dedup:a1b2c3d4...` → `alert-uuid`

## API Endpoints

### Get Alert Details
```http
GET /api/alerts/{id}
```
Returns full alert with lifecycle data, status history, and timestamps.

### Update Status (Generic)
```http
PATCH /api/alerts/{id}/status
Content-Type: application/json

{
  "status": "acknowledged",
  "comment": "Optional transition comment",
  "user": "analyst@example.com"
}
```

### Acknowledge Alert
```http
POST /api/alerts/{id}/acknowledge
```

### Start Investigation
```http
PATCH /api/alerts/{id}/investigate
Content-Type: application/json

{
  "comment": "Starting investigation",
  "user": "analyst@example.com"
}
```

### Resolve Alert
```http
PATCH /api/alerts/{id}/resolve
Content-Type: application/json

{
  "reason": "Issue remediated",
  "comment": "Applied patch to affected server",
  "user": "analyst@example.com"
}
```

### Close Alert
```http
PATCH /api/alerts/{id}/close
Content-Type: application/json

{
  "comment": "Closing investigation",
  "user": "analyst@example.com"
}
```

### Mark as False Positive
```http
PATCH /api/alerts/{id}/false-positive
Content-Type: application/json

{
  "reason": "Benign activity",
  "comment": "Authorized system test",
  "user": "analyst@example.com"
}
```

## Status History

Each status transition is recorded with:
- `timestamp`: When the transition occurred
- `oldStatus`: Previous status
- `newStatus`: New status
- `user`: Who made the change (or "system" for automated)
- `comment`: Optional transition comment

Example:
```json
{
  "statusHistory": [
    {
      "timestamp": "2024-12-01T10:00:00Z",
      "oldStatus": "new",
      "newStatus": "acknowledged",
      "user": "analyst@example.com",
      "comment": "Acknowledged critical alert"
    },
    {
      "timestamp": "2024-12-01T10:15:00Z",
      "oldStatus": "acknowledged",
      "newStatus": "under_investigation",
      "user": "analyst@example.com",
      "comment": null
    }
  ]
}
```

## Deduplication Count

The `alertCount` field tracks how many times a duplicate alert was suppressed:

- Initial alert created: `alertCount = 1`
- Same rule/source/dest triggered again: `alertCount` incremented
- Useful for showing alert frequency in the UI

Example UI display: "Alert triggered 5 times in last hour"

## Automatic Resolution (Future)

The `AutoResolutionService` (to be implemented):
- Scans for alerts with status="new" and lastSeen > 24 hours old
- Transitions them to "resolved" status automatically
- Records as "Auto-resolved due to inactivity"
- Configurable threshold via `AlertLifecycle:StaleAlertThresholdHours`

## Transition Rules

Valid transitions are enforced by `AlertLifecycleService`:

| From | To | Allowed |
|------|-----|---------|
| New | Acknowledged, PendingScore, FalsePositive | ✓ |
| PendingScore | New, Acknowledged, FalsePositive | ✓ |
| Acknowledged | UnderInvestigation, Resolved, FalsePositive | ✓ |
| UnderInvestigation | Resolved, Closed, FalsePositive | ✓ |
| Resolved | Closed, Acknowledged | ✓ |
| Closed | Acknowledged | ✓ |
| FalsePositive | Closed | ✓ |

Invalid transitions throw `InvalidOperationException`.

## Example Workflow

1. **Alert Created**
   - Status: `new`
   - AlertCount: 1
   - FirstSeen: NOW
   - LastSeen: NOW

2. **SOC Analyst Reviews**
   - `PATCH /api/alerts/{id}/status` → `acknowledged`
   - AcknowledgedAt timestamp set

3. **Investigation Ongoing**
   - `PATCH /api/alerts/{id}/investigate`
   - InvestigationStartedAt timestamp set

4. **Issue Remediated**
   - `PATCH /api/alerts/{id}/resolve` with reason="CVE patched"
   - ResolvedAt timestamp set

5. **Alert Closed**
   - `PATCH /api/alerts/{id}/close`
   - ClosedAt timestamp set

## Audit Logging (Future)

All lifecycle transitions are logged to the `audit-log` Kafka topic:

```json
{
  "timestamp": "2024-12-01T10:00:00Z",
  "alertId": "uuid",
  "action": "status_transition",
  "oldStatus": "new",
  "newStatus": "acknowledged",
  "user": "analyst@example.com",
  "correlationId": "trace-id",
  "comment": "Acknowledged critical alert"
}
```

## Performance Considerations

### Indexes
- `ix_alerts_dedup_key`: Fast duplicate detection
- `ix_alerts_status`: Filtering by status
- `ix_alerts_ruleid_lastseen`: Auto-resolution queries
- `ix_alerts_status_severity`: Bulk operations
- `ix_alerts_status_history_gin`: JSONB search (GIN index)

### Deduplication TTL
- Default: 60 minutes
- Balance between noise reduction and responsiveness
- Adjust based on your environment (shorter = more alerts, longer = more dedup)

### Redis Memory
- Dedup keys expire automatically via TTL
- No manual cleanup needed

## Troubleshooting

### "Cannot transition from X to Y"
- Invalid state transition attempted
- Check allowed transitions table above
- Use status endpoint with correct target state

### Deduplication not working
- Verify Redis connection in config
- Check `dedup_key` is populated in database
- Ensure TTL config is set reasonably (not 0)

### Missing status_history
- Check database migration applied
- Verify `status_history` column exists and is JSONB type

## Future Enhancements

1. Bulk status updates (batch transition many alerts)
2. Auto-remediation workflows (integration with SOAR)
3. Alert enrichment during lifecycle (add IOC intel)
4. Lifecycle metrics dashboard (mean time to resolution)
5. Custom transition rules per rule or severity
