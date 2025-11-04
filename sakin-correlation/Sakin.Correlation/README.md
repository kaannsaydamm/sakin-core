# Sakin Correlation - Rule DSL Parser

## Overview

The Sakin Correlation project provides a comprehensive Rule DSL (Domain Specific Language) parser for defining and validating security correlation rules. The parser uses System.Text.Json for serialization/deserialization and JsonSchema.Net for validation.

## Features

- **JSON-based rule definition** with comprehensive schema validation
- **Rich rule model** supporting triggers, conditions, aggregation windows, and actions
- **Extensive validation** with descriptive error messages
- **Flexible parsing** from strings, files, or directories
- **Comprehensive error handling** with detailed exception information
- **Full serialization support** for round-trip operations

## Rule Structure

### Basic Rule Format

```json
{
  "id": "rule-identifier",
  "name": "Human-readable rule name",
  "description": "Detailed description of the rule",
  "enabled": true,
  "severity": "medium",
  "triggers": [...],
  "conditions": [...],
  "aggregation": {...},
  "actions": [...],
  "metadata": {...}
}
```

### Triggers

Triggers define when a rule should be evaluated:

```json
{
  "type": "event|time|threshold",
  "eventType": "event_type_name",
  "source": "optional_source_filter",
  "filters": {
    "key": "value"
  }
}
```

### Conditions

Conditions define the logic that must be satisfied:

```json
{
  "field": "field_name",
  "operator": "equals|contains|greater_than|regex|exists|...",
  "value": "comparison_value",
  "caseSensitive": true,
  "negate": false
}
```

### Aggregation Windows

Aggregation defines how events should be grouped over time:

```json
{
  "type": "time_window|count|sum|average|min|max",
  "size": 5,
  "unit": "seconds|minutes|hours|days",
  "groupBy": ["field1", "field2"],
  "having": {
    "field": "aggregated_field",
    "operator": "greater_than",
    "value": 10
  }
}
```

### Actions

Actions define what happens when a rule triggers:

```json
{
  "type": "alert|webhook|email|script|log|block|quarantine",
  "parameters": {
    "title": "Alert Title",
    "message": "Alert message"
  },
  "delay": 1000,
  "retry": {
    "attempts": 3,
    "delay": 1000,
    "backoff": "exponential"
  }
}
```

## Usage

### Basic Parsing

```csharp
// Setup services
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddSingleton<IRuleValidator, RuleValidator>();
services.AddSingleton<IRuleParser, RuleParser>();

var serviceProvider = services.BuildServiceProvider();
var parser = serviceProvider.GetRequiredService<IRuleParser>();

// Parse a single rule from JSON string
var ruleJson = @"{
  ""id"": ""test-rule"",
  ""name"": ""Test Rule"",
  ""severity"": ""medium"",
  ""triggers"": [
    {
      ""type"": ""event"",
      ""eventType"": ""test_event""
    }
  ]
}";

var rule = await parser.ParseRuleAsync(ruleJson);
```

### Parsing from Files

```csharp
// Parse from a single file
var rule = await parser.ParseRuleFromFileAsync("/path/to/rule.json");

// Parse all rules from a directory
var rules = await parser.ParseRulesFromDirectoryAsync("/path/to/rules/");
```

### Validation

```csharp
var validator = serviceProvider.GetRequiredService<IRuleValidator>();

// Validate JSON syntax
var syntaxResult = validator.ValidateRuleSyntax(ruleJson);

// Full validation
var validationResult = await validator.ValidateRuleAsync(ruleJson);

if (!validationResult.IsValid)
{
    Console.WriteLine($"Validation failed: {validationResult.Message}");
}
```

### Serialization

```csharp
// Serialize a rule back to JSON
var json = parser.SerializeRule(rule);
```

## Error Handling

The parser provides detailed error information:

```csharp
try
{
    var rule = await parser.ParseRuleAsync(invalidJson);
}
catch (RuleParsingException ex)
{
    Console.WriteLine($"Rule ID: {ex.RuleId}");
    Console.WriteLine($"Property Path: {ex.PropertyPath}");
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Example Rules

### Failed Login Detection

```json
{
  "id": "failed-login-attempts",
  "name": "Multiple Failed Login Attempts",
  "severity": "medium",
  "triggers": [
    {
      "type": "event",
      "eventType": "authentication_failure"
    }
  ],
  "conditions": [
    {
      "field": "username",
      "operator": "exists"
    }
  ],
  "aggregation": {
    "type": "count",
    "size": 5,
    "unit": "minutes",
    "groupBy": ["username", "source_ip"],
    "having": {
      "field": "count",
      "operator": "greater_than_or_equal",
      "value": 3
    }
  },
  "actions": [
    {
      "type": "alert",
      "parameters": {
        "title": "Multiple Failed Login Attempts",
        "message": "User {{username}} failed to login {{count}} times"
      }
    }
  ]
}
```

## Schema Validation

All rules are validated against a comprehensive JSON Schema located at:
- `Schemas/correlation-rule-schema.json`

The schema enforces:
- Required fields and data types
- Valid enum values
- Field patterns and constraints
- Nested object structure

## Testing

The project includes comprehensive unit tests covering:
- Valid rule parsing
- Invalid rule rejection with descriptive errors
- Edge cases and error conditions
- Integration tests with sample rules
- Serialization round-trip testing

Run tests with:
```bash
dotnet test tests/Sakin.Correlation.Tests/
```

## Architecture

- **Models**: Core domain objects for rules, triggers, conditions, etc.
- **Parsers**: Logic for parsing and serializing rules
- **Validation**: Schema and business logic validation
- **Exceptions**: Custom exception types for error handling
- **Schemas**: JSON Schema definitions for validation

## Alert Persistence

The correlation engine can persist generated alerts to PostgreSQL for downstream processing and audit requirements. Alert persistence is implemented with Entity Framework Core and exposed via `IAlertRepository` in the `Sakin.Correlation.Persistence` namespace.

### Running Migrations

Database schema changes are tracked through EF Core migrations stored in `Persistence/Migrations`. Use the bundled `dotnet-ef` tool to apply the migrations:

```bash
dotnet tool restore
dotnet ef database update --project sakin-correlation/Sakin.Correlation --context AlertDbContext
```

The `Database` section in `appsettings.json` controls the connection string via the shared `DatabaseOptions` configuration model.

## Dependencies

- `System.Text.Json`: JSON serialization/deserialization
- `JsonSchema.Net`: JSON Schema validation
- `Microsoft.Extensions.Logging`: Logging abstraction
- `Microsoft.Extensions.DependencyInjection`: DI container