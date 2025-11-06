# S.A.K.I.N. Integration Tests

Comprehensive end-to-end (E2E) and integration testing suite for the S.A.K.I.N. platform, validating the full security event processing pipeline.

## Overview

This test suite provides complete coverage of the S.A.K.I.N. platform's core functionality:

- **Event Ingestion & Normalization**: Validates raw event collection and parsing
- **Rule Evaluation**: Tests both stateless and stateful rule processing
- **Alert Lifecycle**: Verifies alert creation, deduplication, and status transitions
- **Data Consistency**: Ensures message ordering, idempotency, and ACID compliance
- **API Contracts**: Validates REST API schemas and response formats
- **Failure Recovery**: Tests system resilience under failure conditions

## Architecture

### Testcontainers Infrastructure

The test suite uses Docker containers (via Testcontainers) to create an isolated test environment:

```
┌─────────────────────────────────────────┐
│   Integration Test Environment           │
├─────────────────────────────────────────┤
│  PostgreSQL  │  Redis  │  Kafka         │
│  (Alerts)    │ (State) │ (Events)       │
└─────────────────────────────────────────┘
```

**Services:**
- **PostgreSQL 16**: Alert storage, rules, assets
- **Redis 7**: Window state, caching, deduplication
- **Kafka 7.6**: Event topics (raw, normalized, alerts)

### Test Fixtures

```
Fixtures/
├── TestContainerEnvironment.cs     # Container orchestration
├── KafkaFixture.cs                 # Kafka producer/consumer setup
├── PostgresFixture.cs              # Database migrations & cleanup
├── RedisFixture.cs                 # Redis connection & operations
└── IntegrationTestFixture.cs       # Combined fixture (xUnit)
```

### Test Scenarios

#### Scenario 1: Simple Event Ingestion & Normalization
**File:** `Scenarios/EventIngestionScenarioTests.cs`

Tests the full ingestion pipeline:
- Raw Syslog messages → raw-events topic
- Event parsing and normalization
- GeoIP enrichment
- Normalized events → normalized-events topic

**Assertions:**
✅ Normalized event created in Kafka
✅ Timestamp correctly parsed
✅ Source IP resolved
✅ No data loss across sources

#### Scenario 2: Stateless Rule Evaluation & Alert Creation
**File:** `Scenarios/StatelessRuleScenarioTests.cs`

Validates simple rule matching:
- Normalized event → Rule evaluation
- Event code == 4625 (failed login)
- Alert creation in PostgreSQL
- Alert visible via Panel API

**Assertions:**
✅ Alert created with correct severity
✅ Evidence (raw event) stored
✅ Multiple rules evaluated
✅ Severity propagation

#### Scenario 3: Stateful Aggregation Rules
**File:** `Scenarios/AggregationRuleScenarioTests.cs`

Tests time-windowed aggregation (brute force detection):
- 15 failed login attempts in 5 minutes
- Grouped by source_ip
- Alert created after 10 attempts within window

**Assertions:**
✅ Alert created after threshold
✅ Time window enforcement
✅ Separate alerts per source IP
✅ AlertCount incremented

#### Scenario 4: Alert Lifecycle & Deduplication
**File:** `Scenarios/AlertLifecycleScenarioTests.cs`

Validates alert lifecycle management:
- Duplicate event handling
- Status transitions (New → Acknowledged → Resolved)
- Counter increments
- Time range tracking (FirstSeen/LastSeen)

**Assertions:**
✅ Only 1 alert created (deduplication)
✅ AlertCount = 10 (duplicates tracked)
✅ Status history maintained
✅ LastSeen updated for each duplicate

#### Scenario 5: Data Consistency
**File:** `DataConsistency/KafkaOrderingTests.cs`

Tests ACID properties and message ordering:
- Kafka partition semantics (same source → same partition)
- Message ordering verification
- Replay idempotency
- No duplicate alerts from same event

**Assertions:**
✅ Events in correct sequence
✅ Same source partitioned correctly
✅ Idempotent processing
✅ Consistent state across replays

#### Scenario 6: API Contracts
**File:** `API/AlertApiContractTests.cs`

Validates REST API schemas:
- GET /api/alerts (pagination, filtering, sorting)
- GET /api/alerts/{id} (full details)
- PATCH /api/alerts/{id}/status (state transitions)
- Bulk operations schemas

**Assertions:**
✅ Correct HTTP status codes
✅ Response schema validation
✅ Error format consistency
✅ Pagination structure

## Running Tests Locally

### Prerequisites

- .NET 8.0 SDK
- Docker & Docker Compose (for container orchestration)
- 4GB RAM available

### Quick Start

```bash
# Restore dependencies
dotnet restore

# Run all integration tests
dotnet test tests/Sakin.Integration.Tests/Sakin.Integration.Tests.csproj

# Run specific test class
dotnet test tests/Sakin.Integration.Tests/Sakin.Integration.Tests.csproj --filter "ClassName=EventIngestionScenarioTests"

# Run with verbose output
dotnet test tests/Sakin.Integration.Tests/Sakin.Integration.Tests.csproj --logger "console;verbosity=detailed"
```

### With Coverage

```bash
# Install coverage tools
dotnet tool install -g dotnet-coverage

# Run tests with coverage
dotnet test tests/Sakin.Integration.Tests/Sakin.Integration.Tests.csproj \
  /p:CollectCoverage=true \
  /p:CoverageFormat=opencover \
  /p:CoverageFilename=coverage.xml

# Generate HTML report
dotnet coverage merge -f xml -o coverage.xml *.xml
```

### Debugging Tests

```bash
# Run specific test with debugging
dotnet test tests/Sakin.Integration.Tests/Sakin.Integration.Tests.csproj \
  --filter "MethodName=BruteForceDetectionAggregation" \
  --logger "console;verbosity=detailed" \
  -vv
```

## Test Configuration

### Environment Variables

```bash
# Optional - defaults to testcontainers-managed services
POSTGRES_CONNECTION_STRING=Host=localhost;Database=sakin_test;...
REDIS_CONNECTION_STRING=localhost:6379
KAFKA_BOOTSTRAP_SERVERS=localhost:9092
```

### Timeout Configuration

- Event processing: 30 seconds
- Aggregation window: 60 seconds
- Message consumption: 30 seconds (configurable per test)

### Parallel Execution

Tests run in parallel by default (xUnit). For sequential execution:

```bash
dotnet test tests/Sakin.Integration.Tests/Sakin.Integration.Tests.csproj -m:1
```

## CI/CD Integration

### GitHub Actions Workflow

Trigger: Push to `main` or PR to `main`

```yaml
# .github/workflows/integration-tests.yml
- Spin up PostgreSQL, Redis, Kafka containers
- Run all integration tests
- Generate coverage reports
- Publish results
- Block merge on failures
```

**Artifacts:**
- Test results (TRX format)
- Coverage reports (OpenCover XML)
- Test logs

## Performance Targets

| Metric | Target | Status |
|--------|--------|--------|
| Full test suite | < 5 minutes | ✅ |
| Single scenario | < 30 seconds | ✅ |
| Zero flaky tests | > 99% pass rate | ✅ |
| Code coverage | > 80% (core) | 📊 |

## Troubleshooting

### Container Issues

```bash
# Clean up containers
docker ps -aq | xargs docker rm -f

# Rebuild test environment
dotnet test tests/Sakin.Integration.Tests/Sakin.Integration.Tests.csproj --no-cache
```

### Kafka Not Available

```bash
# Check Kafka broker
docker ps | grep kafka
docker logs <container_id>

# Verify bootstrap server
kafka-broker-api-versions.sh --bootstrap-server localhost:9092
```

### PostgreSQL Connection Errors

```bash
# Verify PostgreSQL is running
psql -h localhost -U postgres -d sakin_test

# Check migrations ran
SELECT * FROM __EFMigrationsHistory;
```

### Flaky Tests

- Increase timeouts in `AssertionHelpers.WaitForMessageAsync()`
- Reduce parallelism: `dotnet test -m:1`
- Check system resources: CPU, memory, disk I/O

## Test Development Guide

### Adding a New Scenario

1. Create test class in `Scenarios/` directory
2. Inherit from fixture collection:

```csharp
[Collection("Integration Tests")]
public class MyScenarioTests
{
    private readonly IntegrationTestFixture _fixture;
    
    public MyScenarioTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }
}
```

3. Use helpers for common operations:

```csharp
// Produce events
var producer = await _fixture.KafkaFixture.CreateProducerAsync<NormalizedEvent>();
await producer.ProduceAsync("normalized-events", message);

// Consume messages
var consumer = await _fixture.KafkaFixture.CreateConsumerAsync<Alert>(
    "my-test-group",
    new[] { "alerts" });
var alert = await AssertionHelpers.WaitForMessageAsync(consumer);
```

### Best Practices

- ✅ Use `EventFactory` for creating test events
- ✅ Use `TestDataBuilder` for complex scenarios
- ✅ Clean up resources (Dispose producers/consumers)
- ✅ Set appropriate timeouts for async operations
- ✅ Use descriptive test names with `[Fact(DisplayName = "...")]`
- ✅ Include comments explaining the test scenario
- ✅ Verify both positive and negative cases

## Coverage Requirements

**Target: >80% for core services**

### Core Services
- `sakin-ingest`: Parser, normalization logic
- `sakin-correlation`: Rule evaluation, alert creation
- `sakin-panel`: API endpoints, data access

### Excluded
- Infrastructure code (Program.cs, dependency injection)
- Third-party library code
- Generated code

## Known Limitations

1. **No Agent Execution Testing**: SOAR playbook execution with real agents requires separate infra
2. **No GeoIP Validation**: Uses mock GeoIP data (test IPs may not resolve to real locations)
3. **No Threat Intel Validation**: Mock threat intel service
4. **No ClickHouse Testing**: Anomaly detection uses embedded baselines

## Future Enhancements

- [ ] Add SOAR playbook execution tests with agent stubs
- [ ] Add ClickHouse baseline calculation tests
- [ ] Add performance regression tests
- [ ] Add chaos engineering tests
- [ ] Add load testing scenarios
- [ ] Add multi-tenant isolation tests

## Documentation

- [Architecture Overview](../../docs/architecture.md)
- [Rule Development Guide](../../docs/rule-development.md)
- [API Reference](../../docs/api-reference.md)
- [Troubleshooting Guide](../../docs/troubleshooting.md)

## Contributing

See [CONTRIBUTING.md](../../CONTRIBUTING.md) for:
- Code style guide
- Testing conventions
- Commit message format
- Pull request process

## License

See [LICENSE](../../LICENSE)
