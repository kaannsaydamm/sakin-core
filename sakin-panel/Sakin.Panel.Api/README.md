# Sakin Panel API

Backend API for the Sakin security platform panel interface.

## Overview

This ASP.NET Core Web API provides alert management endpoints for the Sakin Panel:
- List alerts with pagination and severity filtering
- Acknowledge alerts to update their status

The API integrates with the `Sakin.Correlation` persistence layer to access and manage alerts stored in PostgreSQL.

## Endpoints

### GET /api/alerts

Retrieves a paginated list of alerts with optional severity filtering.

**Query Parameters:**
- `page` (optional, default: 1): Page number
- `pageSize` (optional, default: 25): Number of items per page
- `severity` (optional): Filter by severity level (`low`, `medium`, `high`, `critical`)

**Response:**
```json
{
  "items": [
    {
      "id": "guid",
      "ruleId": "rule-id",
      "ruleName": "Rule Name",
      "severity": "high",
      "status": "new",
      "triggeredAt": "2024-01-01T00:00:00Z",
      "source": "sensor-1",
      "context": {},
      "matchedConditions": [],
      "aggregationCount": null,
      "aggregatedValue": null,
      "createdAt": "2024-01-01T00:00:00Z",
      "updatedAt": "2024-01-01T00:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 100,
  "totalPages": 4
}
```

### POST /api/alerts/{id}/acknowledge

Acknowledges an alert by updating its status to `acknowledged`.

**Response:**
Returns the updated alert object.

## Configuration

Configure database connection in `appsettings.json`:

```json
{
  "Database": {
    "Host": "localhost",
    "Port": 5432,
    "Database": "correlation_db",
    "Username": "postgres",
    "Password": "postgres"
  }
}
```

## Running the API

```bash
cd sakin-panel/Sakin.Panel.Api
dotnet restore
dotnet run
```

The API will be available at:
- HTTPS: https://localhost:7000 (or configured port)
- HTTP: http://localhost:5000 (or configured port)

Access Swagger UI at: http://localhost:5000/swagger

## Testing

Run unit tests:

```bash
dotnet test tests/Sakin.Panel.Api.Tests/
```

The tests cover:
- Service layer logic (AlertService)
- Controller behavior (AlertsController)
- Pagination calculations
- Error handling
