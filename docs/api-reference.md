# S.A.K.I.N. REST API Reference

## Overview

The S.A.K.I.N. Panel API provides REST endpoints for alert management, rule administration, playbook management, and system monitoring.

**Base URL:** `http://localhost:5000` (development)

**Swagger UI:** `http://localhost:5000/swagger`

## Authentication

### Token-Based Authentication

```bash
# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "admin"}'

# Response
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}

# Use token in requests
curl -H "Authorization: Bearer <token>" http://localhost:5000/api/alerts
```

## Alerts API

### List Alerts

**Endpoint:** `GET /api/alerts`

**Parameters:**
- `page` (query, int): Page number (default: 1)
- `pageSize` (query, int): Results per page (default: 50)
- `severity` (query, string): Filter by severity (Critical, High, Medium, Low, Info)
- `status` (query, string): Filter by status (New, Acknowledged, UnderInvestigation, Resolved, Closed, FalsePositive)
- `ruleId` (query, string): Filter by rule ID
- `sourceIp` (query, string): Filter by source IP
- `hostname` (query, string): Filter by hostname

**Request:**
```bash
curl "http://localhost:5000/api/alerts?severity=High&status=New&pageSize=10"
```

**Response:**
```json
{
  "alerts": [
    {
      "id": "alert-uuid-123",
      "ruleId": "rule-brute-force",
      "ruleName": "Brute Force Detection",
      "severity": "High",
      "status": "New",
      "sourceIp": "192.168.1.100",
      "destinationIp": "192.168.1.1",
      "hostname": "server1",
      "message": "5 failed login attempts in 300 seconds",
      "timestamp": "2024-11-06T10:30:45Z",
      "riskScore": 75,
      "riskLevel": "High",
      "enrichment": {
        "sourceGeo": {
          "country": "United States",
          "city": "New York"
        },
        "threatIntel": {
          "reputation": "malicious",
          "abuseScore": 85
        }
      }
    }
  ],
  "total": 150,
  "page": 1,
  "pageSize": 10
}
```

**Status Codes:**
- `200`: Success
- `400`: Invalid parameters
- `401`: Unauthorized
- `500`: Server error

### Get Alert Details

**Endpoint:** `GET /api/alerts/{alertId}`

**Request:**
```bash
curl "http://localhost:5000/api/alerts/alert-uuid-123"
```

**Response:**
```json
{
  "id": "alert-uuid-123",
  "ruleId": "rule-brute-force",
  "ruleName": "Brute Force Detection",
  "severity": "High",
  "status": "New",
  "sourceIp": "192.168.1.100",
  "destinationIp": "192.168.1.1",
  "hostname": "server1",
  "username": "admin",
  "message": "5 failed login attempts in 300 seconds",
  "timestamp": "2024-11-06T10:30:45Z",
  "riskScore": 75,
  "riskLevel": "High",
  "statusHistory": [
    {
      "status": "New",
      "changedAt": "2024-11-06T10:30:45Z",
      "changedBy": "system"
    }
  ],
  "enrichment": {
    "sourceGeo": {...},
    "destGeo": {...},
    "threatIntel": {...},
    "anomaly": {
      "score": 85,
      "isAnomalous": true,
      "zScore": 3.2
    }
  }
}
```

### Update Alert Status

**Endpoint:** `PUT /api/alerts/{alertId}/status`

**Request Body:**
```json
{
  "status": "Acknowledged",
  "notes": "Investigating this incident"
}
```

**Status Values:**
- `Acknowledged`: Analyst has seen the alert
- `UnderInvestigation`: Active investigation in progress
- `Resolved`: Issue has been resolved
- `Closed`: Alert is closed
- `FalsePositive`: Alert was incorrect

**Request:**
```bash
curl -X PUT http://localhost:5000/api/alerts/alert-uuid-123/status \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"status": "Acknowledged", "notes": "Checking logs"}'
```

**Response:**
```json
{
  "id": "alert-uuid-123",
  "status": "Acknowledged",
  "statusHistory": [
    {
      "status": "New",
      "changedAt": "2024-11-06T10:30:45Z",
      "changedBy": "system"
    },
    {
      "status": "Acknowledged",
      "changedAt": "2024-11-06T10:35:22Z",
      "changedBy": "analyst1"
    }
  ]
}
```

### Bulk Update Alerts

**Endpoint:** `PUT /api/alerts/bulk/status`

**Request Body:**
```json
{
  "alertIds": ["alert-1", "alert-2", "alert-3"],
  "status": "Acknowledged",
  "notes": "Batch acknowledged"
}
```

**Response:**
```json
{
  "updated": 3,
  "failed": 0
}
```

## Rules API

### List Rules

**Endpoint:** `GET /api/rules`

**Parameters:**
- `enabled` (query, bool): Filter by enabled status
- `type` (query, string): Filter by rule type (Stateless, Stateful)
- `severity` (query, string): Filter by severity

**Request:**
```bash
curl "http://localhost:5000/api/rules?enabled=true"
```

**Response:**
```json
{
  "rules": [
    {
      "id": "rule-brute-force",
      "name": "Brute Force Detection",
      "description": "Detects multiple failed login attempts",
      "enabled": true,
      "type": "Stateful",
      "severity": "High",
      "windowSeconds": 300,
      "threshold": 5,
      "alertCount": 1250,
      "lastTriggered": "2024-11-06T10:30:45Z"
    }
  ]
}
```

### Get Rule Details

**Endpoint:** `GET /api/rules/{ruleId}`

**Request:**
```bash
curl "http://localhost:5000/api/rules/rule-brute-force"
```

**Response:**
```json
{
  "id": "rule-brute-force",
  "name": "Brute Force Detection",
  "description": "Detects multiple failed login attempts",
  "enabled": true,
  "type": "Stateful",
  "severity": "High",
  "conditions": [
    {
      "field": "EventType",
      "operator": "Equals",
      "value": "AuthenticationFailure"
    }
  ],
  "groupBy": ["SourceIp", "Username"],
  "threshold": 5,
  "windowSeconds": 300,
  "alertMessage": "Brute force: {threshold} failures from {SourceIp} in {window}s"
}
```

## Playbooks API

### List Playbooks

**Endpoint:** `GET /api/playbooks`

**Request:**
```bash
curl "http://localhost:5000/api/playbooks"
```

**Response:**
```json
{
  "playbooks": [
    {
      "id": "playbook-block-ip",
      "name": "Block Attacker IP",
      "description": "Block source IP on firewall",
      "enabled": true,
      "triggerRules": ["rule-brute-force"],
      "steps": 3,
      "executionCount": 45,
      "lastExecuted": "2024-11-06T10:30:45Z"
    }
  ]
}
```

### Get Playbook Details

**Endpoint:** `GET /api/playbooks/{playbookId}`

**Request:**
```bash
curl "http://localhost:5000/api/playbooks/playbook-block-ip"
```

**Response:**
```json
{
  "id": "playbook-block-ip",
  "name": "Block Attacker IP",
  "description": "Block source IP on firewall",
  "enabled": true,
  "triggerRules": ["rule-brute-force"],
  "steps": [
    {
      "id": "step-1",
      "name": "Notify Team",
      "type": "Notification",
      "channel": "slack"
    },
    {
      "id": "step-2",
      "name": "Block IP",
      "type": "AgentCommand",
      "agent": "firewall-01",
      "command": "block_ip"
    }
  ]
}
```

## Assets API

### List Assets

**Endpoint:** `GET /api/assets`

**Request:**
```bash
curl "http://localhost:5000/api/assets"
```

**Response:**
```json
{
  "assets": [
    {
      "id": "asset-123",
      "hostname": "server1",
      "ipAddress": "192.168.1.10",
      "type": "Server",
      "criticality": "Critical",
      "owner": "ops-team",
      "lastSeen": "2024-11-06T10:30:45Z"
    }
  ]
}
```

## Audit Log API

### Get Audit Events

**Endpoint:** `GET /api/audit`

**Parameters:**
- `action` (query, string): Filter by action (StatusChange, PlaybookExecution, etc)
- `userId` (query, string): Filter by user
- `resourceId` (query, string): Filter by resource

**Request:**
```bash
curl "http://localhost:5000/api/audit?action=StatusChange&limit=50"
```

**Response:**
```json
{
  "events": [
    {
      "id": "audit-123",
      "timestamp": "2024-11-06T10:35:22Z",
      "action": "StatusChange",
      "userId": "analyst1",
      "resourceId": "alert-uuid-123",
      "resourceType": "Alert",
      "details": {
        "oldStatus": "New",
        "newStatus": "Acknowledged",
        "notes": "Checking logs"
      }
    }
  ]
}
```

## Health Check API

### Service Health

**Endpoint:** `GET /healthz`

**Response (200):**
```json
{
  "status": "healthy",
  "services": {
    "database": "healthy",
    "kafka": "healthy",
    "redis": "healthy"
  },
  "timestamp": "2024-11-06T10:30:45Z"
}
```

## Error Responses

All errors follow this format:

```json
{
  "error": "Error message",
  "code": "ERROR_CODE",
  "details": "Additional details if available"
}
```

**Common Error Codes:**
- `INVALID_REQUEST`: Invalid request parameters
- `UNAUTHORIZED`: Authentication required
- `NOT_FOUND`: Resource not found
- `CONFLICT`: Resource already exists
- `INTERNAL_ERROR`: Server error

**Examples:**

```bash
# 400 Bad Request
{
  "error": "Invalid severity value",
  "code": "INVALID_REQUEST",
  "details": "Valid values: Critical, High, Medium, Low, Info"
}

# 401 Unauthorized
{
  "error": "Authentication required",
  "code": "UNAUTHORIZED"
}

# 404 Not Found
{
  "error": "Alert not found",
  "code": "NOT_FOUND"
}
```

## Rate Limiting

- **Rate Limit**: 1000 requests/minute per API key
- **Headers**:
  - `X-RateLimit-Limit`: Request limit
  - `X-RateLimit-Remaining`: Remaining requests
  - `X-RateLimit-Reset`: Reset time (Unix timestamp)

## Pagination

All list endpoints support pagination:

```bash
curl "http://localhost:5000/api/alerts?page=2&pageSize=25"
```

**Default Values:**
- `page`: 1
- `pageSize`: 50
- `max pageSize`: 500

## Filtering

### Common Filters

**Severity:** `Critical` | `High` | `Medium` | `Low` | `Info`

**Status:** `New` | `Acknowledged` | `UnderInvestigation` | `Resolved` | `Closed` | `FalsePositive`

**Rule Type:** `Stateless` | `Stateful`

**Criticality:** `Critical` | `High` | `Medium` | `Low`

## Example Workflows

### Workflow 1: Acknowledge and Investigate Alert

```bash
# 1. Get token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "analyst1", "password": "pass"}' \
  | jq -r '.token')

# 2. Get high-severity alerts
curl -s "http://localhost:5000/api/alerts?severity=High&status=New" \
  -H "Authorization: Bearer $TOKEN" | jq .

# 3. Acknowledge alert
curl -X PUT http://localhost:5000/api/alerts/alert-123/status \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"status": "UnderInvestigation", "notes": "Investigating potential breach"}'
```

### Workflow 2: Get Audit Trail

```bash
# Get all status changes for specific alert
curl "http://localhost:5000/api/audit?resourceId=alert-123&action=StatusChange" \
  -H "Authorization: Bearer $TOKEN" | jq .
```

---

**See Also:**
- [Swagger UI](http://localhost:5000/swagger) — Interactive API documentation
- [Quickstart](./quickstart.md) — Get started in 5 minutes
- [Architecture](./architecture.md) — System design
