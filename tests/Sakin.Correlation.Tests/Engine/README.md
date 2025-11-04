# Rule Evaluation Engine Tests

This directory contains comprehensive tests for the rule evaluation engine that simulates event streams for security correlation rules.

## Test Structure

### RuleEvaluatorTests.cs
Basic unit tests for the `RuleEvaluator` class covering:
- Rule enabling/disabling
- Event normalization checks
- Trigger matching
- Condition evaluation (exists, equals, regex, numeric comparisons, lists)
- Time-based conditions (hour_of_day, day_of_week)
- Negated conditions
- Multiple condition validation

### RuleAggregationTests.cs
Tests for aggregation logic covering:
- Count aggregations with thresholds
- Event grouping by fields
- Deterministic evaluation (same inputs -> same outputs)
- Empty event streams
- Multiple groups evaluation
- Theory-based threshold testing

### RuleEvaluationStreamTests.cs
Integration tests simulating real-world event streams:

#### Brute Force Attack Detection
- Multiple failed login attempts from same source
- Insufficient attempts (negative test)
- Different users from same IP (grouping test)
- Same user from different IPs (grouping test)

#### Suspicious DNS Query Detection  
- Malicious domain detection via regex
- Benign domains (negative test)
- Mixed malicious and benign queries

#### Data Exfiltration Detection
- Large outbound data transfers
- Small transfers below threshold (negative test)
- Different hosts transferring data (grouping test)

#### Rare Hour Access Detection
- File access during unusual hours (0-5, 22-23)
- Normal hour access (negative test)
- Weekend access exclusion

#### Malware Detection
- Suspicious unsigned executables
- Signed processes (negative test)

#### Complex Stream Scenarios
- Mixed event types
- Timestamp-ordered events
- Multi-rule evaluation on single stream

## Test Features

### In-Memory Event Streams
Tests use in-memory collections of `EventEnvelope` objects rather than actual Kafka/Redis connections, ensuring:
- **Deterministic outcomes**: Tests produce consistent results
- **Fast execution**: No external dependencies
- **Isolation**: Each test is independent
- **Reproducibility**: Easy to debug and maintain

### Mock-based Architecture
- Uses Moq for logger mocking
- No external service dependencies
- Suitable for CI/CD pipeline execution

### Comprehensive Coverage
- Positive and negative test cases
- Edge cases (empty streams, boundary values)
- Grouping and aggregation logic
- Time-window validation
- Field mapping and metadata extraction

## Rule Files Tested

The tests validate against rule definitions in `/configs/rules/`:
- `failed-login-attempts.json` - Brute force detection
- `suspicious-dns-query.json` - Malicious DNS queries
- `data-exfiltration.json` - Large data transfers
- `rare-hour-access.json` - Unusual time access
- `malware-detection.json` - Unsigned executable detection

## Running Tests

```bash
# Run all evaluation engine tests
dotnet test --filter "FullyQualifiedName~Engine"

# Run specific test class
dotnet test --filter "FullyQualifiedName~RuleAggregationTests"

# Run with detailed output
dotnet test --filter "FullyQualifiedName~Engine" --logger "console;verbosity=detailed"
```

## Continuous Integration

These tests are designed to run in CI pipelines:
- No external dependencies required
- Fast execution (< 2 seconds for full suite)
- Deterministic results ensure regression detection
- Clear failure messages for debugging

## Test Patterns

### Event Stream Generation
```csharp
var events = GenerateBruteForceEventStream(
    username: "admin",
    sourceIp: "192.168.1.100",
    attemptCount: 5,
    timeSpanMinutes: 4
);
```

### Rule Loading
```csharp
var rule = await LoadRuleAsync("failed-login-attempts");
```

### Evaluation
```csharp
var result = await _evaluator.EvaluateWithAggregationAsync(rule, events);

result.IsMatch.Should().BeTrue();
result.ShouldTriggerAlert.Should().BeTrue();
result.AggregationCount.Should().BeGreaterOrEqualTo(3);
```

## Future Enhancements

- Add performance benchmarks
- Include time-window sliding tests
- Add correlation between multiple rules
- Test rule chaining scenarios
- Add fuzzing tests for robustness
