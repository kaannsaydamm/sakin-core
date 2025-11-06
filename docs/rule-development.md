# Rule Development Guide

## Overview

S.A.K.I.N. uses JSON-based rules for event correlation and threat detection. Rules are evaluated against normalized events in real-time, generating alerts when conditions match.

## Rule Types

### 1. Stateless Rules

Single-event pattern matching with immediate alert generation.

**Use Cases:**
- High-severity events
- Privilege escalations
- Admin account activities
- Suspicious processes

**Example:**
```json
{
  "Id": "rule-admin-privilege-escalation",
  "Name": "Admin Privilege Escalation",
  "Description": "Detects privilege escalation to admin",
  "Enabled": true,
  "RuleType": "Stateless",
  "Conditions": [
    {
      "Field": "EventType",
      "Operator": "Equals",
      "Value": "PrivilegeEscalation"
    },
    {
      "Field": "TargetUser",
      "Operator": "Equals",
      "Value": "Administrator"
    }
  ],
  "Severity": "Critical",
  "AlertMessage": "Privilege escalation to admin by {Username}"
}
```

### 2. Stateful Rules

Time-windowed aggregation with Redis-backed state management.

**Use Cases:**
- Brute-force attacks (count failures)
- Data exfiltration (volume threshold)
- Port scanning (unique port count)
- Authentication patterns

**Example:**
```json
{
  "Id": "rule-brute-force-ssh",
  "Name": "SSH Brute Force Detection",
  "Description": "Detects multiple failed SSH login attempts",
  "Enabled": true,
  "RuleType": "Stateful",
  "WindowSeconds": 300,
  "Conditions": [
    {
      "Field": "EventType",
      "Operator": "Equals",
      "Value": "AuthenticationFailure"
    },
    {
      "Field": "Service",
      "Operator": "Equals",
      "Value": "SSH"
    }
  ],
  "GroupBy": ["SourceIp", "Hostname"],
  "Threshold": 5,
  "Severity": "High",
  "AlertMessage": "SSH brute force: {count} failures from {SourceIp} to {Hostname} in {window}s"
}
```

## Rule Structure

### Required Fields

```json
{
  "Id": "unique-rule-id",
  "Name": "Human-Readable Rule Name",
  "Description": "Detailed description",
  "Enabled": true|false,
  "RuleType": "Stateless|Stateful",
  "Conditions": [...],
  "Severity": "Critical|High|Medium|Low|Info",
  "AlertMessage": "Message template"
}
```

### Optional Fields (Stateful)

```json
{
  "WindowSeconds": 300,
  "GroupBy": ["Field1", "Field2"],
  "Threshold": 5,
  "AlertOn": "threshold|minimum|maximum"
}
```

## Conditions

### Condition Structure

```json
{
  "Field": "EventFieldName",
  "Operator": "Equals|Contains|...",
  "Value": "comparison value",
  "Not": false
}
```

### Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `Equals` | Exact match | `Service` = `SSH` |
| `NotEquals` | Not equal | `Username` ≠ `admin` |
| `Contains` | Substring match | `Message` contains `error` |
| `NotContains` | No substring | `Message` doesn't contain `success` |
| `GreaterThan` | Numeric > | `Severity` > 5 |
| `LessThan` | Numeric < | `DurationMs` < 1000 |
| `GreaterThanOrEqual` | Numeric >= | `FailureCount` >= 3 |
| `LessThanOrEqual` | Numeric <= | `Port` <= 1024 |
| `StartsWith` | String prefix | `Hostname` starts with `prod-` |
| `EndsWith` | String suffix | `Hostname` ends with `.internal` |
| `In` | List membership | `Severity` in [Critical, High] |
| `NotIn` | Not in list | `Country` not in [US, CA] |
| `Regex` | Regular expression | `Url` matches `.*\.exe$` |
| `Exists` | Field is present | `Username` exists |
| `NotExists` | Field missing | `MfaToken` doesn't exist |

### Condition Examples

**Single Condition:**
```json
{
  "Field": "EventType",
  "Operator": "Equals",
  "Value": "PrivilegeEscalation"
}
```

**Multiple Conditions (AND):**
```json
"Conditions": [
  {
    "Field": "EventType",
    "Operator": "Equals",
    "Value": "PrivilegeEscalation"
  },
  {
    "Field": "Username",
    "Operator": "Equals",
    "Value": "admin"
  }
]
```

**Negation:**
```json
{
  "Field": "Hostname",
  "Operator": "NotContains",
  "Value": "test-"
}
```

**Complex Pattern:**
```json
{
  "Field": "Url",
  "Operator": "Regex",
  "Value": ".*\\.exe$|.*\\.ps1$|.*\\.cmd$"
}
```

## Event Fields

### Available Fields for Conditions

**Network Fields:**
- `SourceIp`: Source IP address
- `DestinationIp`: Destination IP address
- `SourcePort`: Source port
- `DestinationPort`: Destination port
- `Protocol`: Network protocol (TCP, UDP, DNS, etc)

**Event Fields:**
- `EventType`: Type of event (AuthenticationFailure, PrivilegeEscalation, etc)
- `Severity`: Event severity (Critical, High, Medium, Low, Info)
- `Timestamp`: Event timestamp
- `Message`: Event message

**Authentication Fields:**
- `Username`: Source username
- `TargetUser`: Destination/target user
- `Service`: Service accessed (SSH, RDP, HTTP, etc)
- `Hostname`: Source hostname
- `TargetHostname`: Destination hostname

**Application Fields:**
- `Url`: HTTP URL
- `Domain`: Domain name
- `UserAgent`: HTTP user agent
- `Method`: HTTP method

**Enrichment Fields:**
- `SourceCountry`: GeoIP country
- `SourceCity`: GeoIP city
- `DestCountry`: Destination country
- `ThreatIntelRep`: Threat intelligence reputation
- `AbuseScore`: IP abuse score

## Alert Message Templates

Use field names in curly braces to insert values:

```json
"AlertMessage": "Brute force: {count} failures from {SourceIp} to {Hostname} in {window}s"
```

**Available Variables:**
- Event fields: `{FieldName}`
- Stateful: `{count}`, `{window}`, `{threshold}`
- Special: `{timestamp}`, `{severity}`

## Severity Levels

Choose appropriate severity based on impact:

| Severity | Example | Alert | Score |
|----------|---------|-------|-------|
| `Info` | Login success, file access | Yes | 1-2 |
| `Low` | Policy violation, config change | Yes | 3-4 |
| `Medium` | Failed auth attempt, port scan | Yes | 5-6 |
| `High` | Brute force, data transfer | Yes | 7-8 |
| `Critical` | Privilege escalation, malware | Yes | 9-10 |

## Rule Examples

### Example 1: Brute-Force SSH

```json
{
  "Id": "rule-brute-force-ssh",
  "Name": "SSH Brute Force Detection",
  "Description": "Detects multiple failed SSH login attempts within 5 minutes",
  "Enabled": true,
  "RuleType": "Stateful",
  "Conditions": [
    {
      "Field": "EventType",
      "Operator": "Equals",
      "Value": "AuthenticationFailure"
    },
    {
      "Field": "Service",
      "Operator": "Equals",
      "Value": "SSH"
    }
  ],
  "GroupBy": ["SourceIp", "TargetHostname"],
  "Threshold": 5,
  "WindowSeconds": 300,
  "Severity": "High",
  "AlertMessage": "SSH brute force detected: {count} failures from {SourceIp} to {TargetHostname} in {WindowSeconds}s"
}
```

### Example 2: Privilege Escalation

```json
{
  "Id": "rule-privilege-escalation",
  "Name": "Local Privilege Escalation",
  "Description": "Detects attempts to escalate privileges",
  "Enabled": true,
  "RuleType": "Stateless",
  "Conditions": [
    {
      "Field": "EventType",
      "Operator": "Equals",
      "Value": "PrivilegeEscalation"
    },
    {
      "Field": "TargetUser",
      "Operator": "Equals",
      "Value": "Administrator"
    }
  ],
  "Severity": "Critical",
  "AlertMessage": "Privilege escalation to admin: {Username} on {Hostname}"
}
```

### Example 3: Large Data Transfer

```json
{
  "Id": "rule-data-exfiltration",
  "Name": "Large Data Transfer Detected",
  "Description": "Detects unusually large outbound data transfers",
  "Enabled": true,
  "RuleType": "Stateful",
  "Conditions": [
    {
      "Field": "BytesSent",
      "Operator": "GreaterThan",
      "Value": 1073741824
    },
    {
      "Field": "DestinationCountry",
      "Operator": "NotIn",
      "Value": ["US", "CA", "GB"]
    }
  ],
  "GroupBy": ["SourceIp", "Username"],
  "Threshold": 1,
  "WindowSeconds": 3600,
  "Severity": "Critical",
  "AlertMessage": "Large data transfer detected: {BytesSent} bytes from {Username} to {DestinationCountry}"
}
```

### Example 4: Port Scanning

```json
{
  "Id": "rule-port-scanning",
  "Name": "Port Scanning Activity",
  "Description": "Detects scanning behavior (many ports in short time)",
  "Enabled": true,
  "RuleType": "Stateful",
  "Conditions": [
    {
      "Field": "EventType",
      "Operator": "Equals",
      "Value": "ConnectionAttempt"
    },
    {
      "Field": "Status",
      "Operator": "Equals",
      "Value": "Rejected"
    }
  ],
  "GroupBy": ["SourceIp", "TargetHostname"],
  "Threshold": 20,
  "WindowSeconds": 60,
  "Severity": "Medium",
  "AlertMessage": "Port scanning detected: {count} connection attempts from {SourceIp} in {window}s"
}
```

### Example 5: Anomalous API Activity

```json
{
  "Id": "rule-anomalous-api",
  "Name": "Anomalous API Activity",
  "Description": "Detects unusual API endpoint access patterns",
  "Enabled": true,
  "RuleType": "Stateless",
  "Conditions": [
    {
      "Field": "Url",
      "Operator": "Contains",
      "Value": "/api/"
    },
    {
      "Field": "HttpStatus",
      "Operator": "In",
      "Value": [401, 403, 429]
    },
    {
      "Field": "UserAgent",
      "Operator": "Exists",
      "Not": true
    }
  ],
  "Severity": "Medium",
  "AlertMessage": "Anomalous API activity: {HttpStatus} from {SourceIp} to {Url}"
}
```

## Deployment

### File Structure

```
sakin-correlation/
├── rules/
│   ├── authentication.json
│   ├── privilege-escalation.json
│   ├── data-exfiltration.json
│   ├── malware-detection.json
│   └── network-reconnaissance.json
```

### Adding Rules

1. Create JSON file in `sakin-correlation/rules/`
2. Define complete rule structure
3. Validate JSON syntax
4. Restart correlation service or reload via API

```bash
# Validate JSON
jq empty rule-name.json

# Deploy
cp rule-name.json sakin-correlation/rules/

# Restart service
docker restart correlation
```

### Rule Loading

```bash
# Check loaded rules
curl http://localhost:5000/api/rules

# Get specific rule
curl http://localhost:5000/api/rules/rule-brute-force-ssh
```

## Testing Rules

### Manual Testing

```bash
# Send test event
curl -X POST http://localhost:5000/api/events \
  -H "Content-Type: application/json" \
  -d '{
    "EventType": "AuthenticationFailure",
    "SourceIp": "192.168.1.100",
    "Service": "SSH",
    "Hostname": "server1"
  }'

# Check alerts
curl http://localhost:5000/api/alerts
```

### Query Rule Performance

```bash
# Get rule metrics
curl "http://localhost:5000/api/rules/rule-id/metrics"

# Response
{
  "ruleId": "rule-brute-force-ssh",
  "alertCount": 450,
  "lastTriggered": "2024-11-06T10:30:45Z",
  "averageProcessingMs": 2.5
}
```

## Best Practices

### 1. Clear Naming

```json
✓ "rule-ssh-brute-force"
✗ "brute_force"
✗ "rule1"
```

### 2. Specific Conditions

```json
✓ Multiple conditions narrowing scope
✗ Overly broad, high false positive rate
```

### 3. Appropriate Severity

```json
✓ Critical for privilege escalation
✓ High for confirmed attacks
✓ Medium for suspicious patterns
✗ Everything Critical
```

### 4. Meaningful Alert Messages

```json
✓ "SSH brute force: 15 failures from 10.1.1.1 to prod-server in 300s"
✗ "Alert triggered"
```

### 5. Use Aggregation Wisely

```json
✓ Stateful for patterns over time
✗ Stateful for simple matches
```

### 6. Consider False Positives

Add exclusions where applicable:

```json
{
  "Conditions": [
    {
      "Field": "SourceIp",
      "Operator": "NotIn",
      "Value": ["10.0.0.50", "10.0.0.51"]
    }
  ]
}
```

## Advanced Topics

### Composite Rules (Future)

```json
{
  "RuleType": "Composite",
  "Rules": [
    "rule-brute-force-ssh",
    "rule-privilege-escalation"
  ],
  "TimeWindow": 3600,
  "MatchCount": 2,
  "AlertMessage": "Multi-stage attack detected"
}
```

### ML-Based Rules (Future)

```json
{
  "RuleType": "ML",
  "Model": "anomaly-detection-v2",
  "Threshold": 0.85,
  "Features": ["BytesSent", "ConnectionCount", "TimeOfDay"]
}
```

## Troubleshooting

### Rule Not Triggering

1. Verify rule is enabled
2. Check conditions match event fields
3. Review rule evaluation logs
4. Test with sample events

### High False Positive Rate

1. Make conditions more specific
2. Add field filters/exclusions
3. Increase threshold for stateful rules
4. Review similar working rules

### Performance Issues

1. Avoid complex regex patterns
2. Use indexed fields in GroupBy
3. Keep window sizes reasonable
4. Monitor rule evaluation latency

---

**See Also:**
- [Alert Lifecycle Management](./alert-lifecycle.md)
- [SOAR Playbooks](./sprint7-soar.md)
- [Quickstart](./quickstart.md)
