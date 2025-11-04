# Panel Alerts API Delivery

This document summarizes the implementation of the panel alert endpoint feature.

## Objective
Extend sakin-panel backend to expose /api/alerts list and acknowledge endpoints, integrating with the Alerts table using shared models and providing pagination and filtering by severity.

## Changes Made

### 1. Alert Status Model
**New File:** `sakin-correlation/Sakin.Correlation/Models/AlertStatus.cs`
- Added `AlertStatus` enum with values: `New`, `Acknowledged`, `Resolved`
- Uses JSON serialization attributes for proper API responses

### 2. Database Schema Updates
**Modified:** `sakin-correlation/Sakin.Correlation/Persistence/Entities/AlertEntity.cs`
- Added `Status` property with default value "new"

**Modified:** `sakin-correlation/Sakin.Correlation/Persistence/Models/AlertRecord.cs`
- Added `Status` property of type `AlertStatus`

**Modified:** `sakin-correlation/Sakin.Correlation/Persistence/AlertDbContext.cs`
- Added configuration for `Status` column with max length 32
- Added default value constraint
- Added index on Status column for query optimization

**New Migration:** `20241104120001_AddAlertStatusField.cs`
- Adds `status` column to alerts table
- Creates index `ix_alerts_status`
- Includes up and down migrations

**Modified:** Model snapshot to include Status property

### 3. Repository Layer Extensions
**Modified:** `sakin-correlation/Sakin.Correlation/Persistence/Repositories/IAlertRepository.cs`
- Added `GetAlertsAsync` method with pagination support
  - Parameters: page, pageSize, severity filter
  - Returns: tuple of (alerts list, total count)
- Added `UpdateStatusAsync` method for status updates
  - Parameters: alert ID, new status
  - Returns: updated AlertRecord or null

**Modified:** `sakin-correlation/Sakin.Correlation/Persistence/Repositories/AlertRepository.cs`
- Implemented `GetAlertsAsync` with efficient pagination using Skip/Take
- Implemented `UpdateStatusAsync` with optimistic update tracking
- Added status serialization/deserialization helpers (`ToStatusString`, `ParseStatus`)
- Updated entity mapping to include Status field

### 4. Panel API Project
**New Project:** `sakin-panel/Sakin.Panel.Api`
- ASP.NET Core Web API project targeting .NET 8.0
- References `Sakin.Correlation` project for persistence layer

**Structure:**
```
Sakin.Panel.Api/
├── Controllers/
│   └── AlertsController.cs
├── Models/
│   ├── AlertResponse.cs
│   └── PaginatedResponse.cs
├── Services/
│   ├── IAlertService.cs
│   └── AlertService.cs
├── Program.cs
├── appsettings.json
└── Sakin.Panel.Api.csproj
```

#### Controllers
**File:** `Controllers/AlertsController.cs`
- `GET /api/alerts` endpoint
  - Query parameters: page, pageSize, severity
  - Returns paginated alert responses
  - Validates severity parameter
  - Returns 400 Bad Request for invalid severity values
- `POST /api/alerts/{id}/acknowledge` endpoint
  - Acknowledges alert by GUID
  - Returns 404 Not Found if alert doesn't exist
  - Returns updated alert on success

#### Models
**File:** `Models/AlertResponse.cs`
- DTO for API responses
- Includes all alert fields with proper serialization
- Static factory method `FromRecord` for mapping from AlertRecord

**File:** `Models/PaginatedResponse.cs`
- Generic pagination response wrapper
- Properties: Items, Page, PageSize, TotalCount, TotalPages

#### Services
**File:** `Services/IAlertService.cs`
- Interface defining alert service operations
- Methods: GetAlertsAsync, GetAlertByIdAsync, AcknowledgeAlertAsync

**File:** `Services/AlertService.cs`
- Service layer implementation
- Handles business logic and repository interaction
- Calculates total pages for pagination
- Maps repository models to API responses

#### Configuration
**File:** `Program.cs`
- Configures ASP.NET Core pipeline
- Registers services and controllers
- Adds Swagger/OpenAPI support
- Integrates correlation persistence layer

**Files:** `appsettings.json`, `appsettings.Development.json`
- Database connection configuration
- Default PostgreSQL settings for correlation_db

### 5. Unit Tests
**New Project:** `tests/Sakin.Panel.Api.Tests`
- xUnit test project with Moq and FluentAssertions
- References Sakin.Panel.Api project

**File:** `tests/Sakin.Panel.Api.Tests/Services/AlertServiceTests.cs`
- Tests for AlertService class
- Coverage includes:
  - Paginated alert retrieval
  - Severity filtering
  - Alert retrieval by ID
  - Alert acknowledgement
  - Edge cases (null returns, pagination calculation)
- 8 test cases

**File:** `tests/Sakin.Panel.Api.Tests/Controllers/AlertsControllerTests.cs`
- Tests for AlertsController
- Coverage includes:
  - Valid alert listing requests
  - Severity filter validation
  - Invalid severity handling
  - Alert acknowledgement success and failure
- 5 test cases

### 6. Integration Updates
**Modified:** `tests/Sakin.Correlation.Tests/Integration/AlertRepositoryIntegrationTests.cs`
- Updated test data to include Status = AlertStatus.New for all created alerts
- Ensures compatibility with new required Status field

**Modified:** `SAKINCore-CS.sln`
- Added Sakin.Panel.Api project to solution
- Added Sakin.Panel.Api.Tests project to solution
- Configured build and nested project settings

### 7. Documentation
**Modified:** `sakin-panel/README.md`
- Updated status section to reflect new backend API
- Added API endpoint documentation
- Added instructions for running the API

**New File:** `sakin-panel/Sakin.Panel.Api/README.md`
- Comprehensive API documentation
- Endpoint specifications with examples
- Configuration instructions
- Running and testing instructions

## Features Implemented

### ✅ Alert Listing Endpoint
- `GET /api/alerts` with pagination support
- Optional severity filtering (low, medium, high, critical)
- Returns structured paginated responses with metadata
- Proper error handling for invalid parameters

### ✅ Alert Acknowledgement Endpoint
- `POST /api/alerts/{id}/acknowledge` 
- Updates alert status to "acknowledged"
- Returns updated alert or 404 if not found
- Tracks update timestamp

### ✅ Pagination
- Configurable page size (default: 25)
- Calculates total pages
- Efficient database queries with Skip/Take
- Handles edge cases (page <= 0, pageSize <= 0)

### ✅ Severity Filtering
- Supports filtering by any SeverityLevel enum value
- Case-insensitive parsing
- Validation with proper error messages

### ✅ Unit Tests
- 13 tests total (8 service + 5 controller)
- All tests passing
- Comprehensive coverage of success and error paths
- Mocked dependencies using Moq

## Technology Stack
- ASP.NET Core 8.0 Web API
- Entity Framework Core 8.0
- PostgreSQL (via Npgsql)
- xUnit + Moq + FluentAssertions for testing
- Swagger/OpenAPI for API documentation

## Database Schema
New `status` column in `alerts` table:
- Type: character varying(32)
- Default: 'new'
- Indexed for query performance
- Not nullable

## API Response Examples

### List Alerts
```bash
GET /api/alerts?page=1&pageSize=10&severity=high
```

Response:
```json
{
  "items": [...],
  "page": 1,
  "pageSize": 10,
  "totalCount": 42,
  "totalPages": 5
}
```

### Acknowledge Alert
```bash
POST /api/alerts/123e4567-e89b-12d3-a456-426614174000/acknowledge
```

Response:
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "ruleId": "failed-login",
  "ruleName": "Failed Login Detection",
  "severity": "high",
  "status": "acknowledged",
  ...
}
```

## Testing
```bash
# Build solution
dotnet build

# Run Panel API tests
dotnet test tests/Sakin.Panel.Api.Tests/

# Run API
cd sakin-panel/Sakin.Panel.Api
dotnet run
```

## Acceptance Criteria Met
- ✅ API returns seeded alerts with pagination and filtering
- ✅ Acknowledgement endpoint updates status in database
- ✅ Unit tests cover controller and service layers
- ✅ Integration with shared Sakin.Correlation models
- ✅ Proper error handling and validation
- ✅ Swagger documentation available
