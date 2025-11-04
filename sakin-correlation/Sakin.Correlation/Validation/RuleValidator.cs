using Json.Schema;
using Microsoft.Extensions.Logging;
using Sakin.Correlation.Exceptions;
using Sakin.Correlation.Models;
using System.Text.Json;

namespace Sakin.Correlation.Validation;

public interface IRuleValidator
{
    Task<ValidationResult> ValidateRuleAsync(string ruleJson);
    Task<ValidationResult> ValidateRuleAsync(CorrelationRule rule);
    ValidationResult ValidateRuleSyntax(string ruleJson);
}

public class RuleValidator : IRuleValidator
{
    private readonly JsonSchema _schema;
    private readonly ILogger<RuleValidator> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RuleValidator(ILogger<RuleValidator> logger)
    {
        _logger = logger;
        _schema = LoadSchema();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    public async Task<ValidationResult> ValidateRuleAsync(string ruleJson)
    {
        try
        {
            // First validate JSON syntax
            var syntaxResult = ValidateRuleSyntax(ruleJson);
            if (!syntaxResult.IsValid)
            {
                return syntaxResult;
            }

            // Parse the rule
            var rule = JsonSerializer.Deserialize<CorrelationRule>(ruleJson, _jsonOptions);
            if (rule == null)
            {
                return new ValidationResult(false, "Failed to deserialize rule", "root");
            }

            return await ValidateRuleAsync(rule);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error");
            return new ValidationResult(false, $"JSON parsing error: {ex.Message}", "root");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected validation error");
            return new ValidationResult(false, $"Unexpected error: {ex.Message}", "root");
        }
    }

    public Task<ValidationResult> ValidateRuleAsync(CorrelationRule rule)
    {
        try
        {
            var errors = new List<string>();

            // Validate basic required fields
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                errors.Add("Rule ID is required");
            }
            else if (!System.Text.RegularExpressions.Regex.IsMatch(rule.Id, @"^[a-zA-Z0-9_-]+$"))
            {
                errors.Add("Rule ID can only contain alphanumeric characters, underscores, and hyphens");
            }

            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                errors.Add("Rule name is required");
            }

            // Validate triggers
            if (rule.Triggers == null || rule.Triggers.Count == 0)
            {
                errors.Add("At least one trigger is required");
            }
            else
            {
                foreach (var trigger in rule.Triggers)
                {
                    var triggerErrors = ValidateTrigger(trigger);
                    errors.AddRange(triggerErrors);
                }
            }

            // Validate conditions
            if (rule.Conditions != null)
            {
                foreach (var condition in rule.Conditions)
                {
                    var conditionErrors = ValidateCondition(condition);
                    errors.AddRange(conditionErrors);
                }
            }

            // Validate aggregation
            if (rule.Aggregation != null)
            {
                var aggregationErrors = ValidateAggregation(rule.Aggregation);
                errors.AddRange(aggregationErrors);
            }

            // Validate actions
            if (rule.Actions != null)
            {
                foreach (var action in rule.Actions)
                {
                    var actionErrors = ValidateAction(action);
                    errors.AddRange(actionErrors);
                }
            }

            return Task.FromResult(errors.Count == 0 
                ? new ValidationResult(true, "Rule is valid")
                : new ValidationResult(false, string.Join("; ", errors)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rule validation error");
            return Task.FromResult(new ValidationResult(false, $"Validation error: {ex.Message}"));
        }
    }

    public ValidationResult ValidateRuleSyntax(string ruleJson)
    {
        try
        {
            using var document = JsonDocument.Parse(ruleJson);
            
            // Basic JSON syntax validation - if we can parse it, it's syntactically valid
            // Schema validation will happen in the full validation method
            return new ValidationResult(true, "Rule JSON syntax is valid");
        }
        catch (JsonException ex)
        {
            return new ValidationResult(false, $"Invalid JSON: {ex.Message}", "root");
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Validation error: {ex.Message}", "root");
        }
    }

    private JsonSchema LoadSchema()
    {
        // Try multiple possible locations for the schema file
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Schemas", "correlation-rule-schema.json"),
            Path.Combine("Schemas", "correlation-rule-schema.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "sakin-correlation", "Sakin.Correlation", "Schemas", "correlation-rule-schema.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "sakin-correlation", "Sakin.Correlation", "Schemas", "correlation-rule-schema.json")
        };

        string? schemaPath = null;
        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                schemaPath = path;
                break;
            }
        }

        if (schemaPath == null)
        {
            // As a fallback, embed the schema as a string
            return LoadEmbeddedSchema();
        }

        var schemaJson = File.ReadAllText(schemaPath);
        return JsonSchema.FromText(schemaJson);
    }

    private JsonSchema LoadEmbeddedSchema()
    {
        // Embedded schema as fallback
        var schemaJson = @"{
  ""$schema"": ""http://json-schema.org/draft-07/schema#"",
  ""$id"": ""https://sakin.io/schemas/correlation-rule.json"",
  ""title"": ""Correlation Rule"",
  ""description"": ""Schema for correlation rule definitions"",
  ""type"": ""object"",
  ""required"": [""id"", ""name"", ""severity"", ""triggers""],
  ""properties"": {
    ""id"": {
      ""type"": ""string"",
      ""pattern"": ""^[a-zA-Z0-9_-]+$"",
      ""minLength"": 1,
      ""maxLength"": 100
    },
    ""name"": {
      ""type"": ""string"",
      ""minLength"": 1,
      ""maxLength"": 200
    },
    ""description"": {
      ""type"": ""string"",
      ""maxLength"": 1000
    },
    ""enabled"": {
      ""type"": ""boolean"",
      ""default"": true
    },
    ""severity"": {
      ""type"": ""string"",
      ""enum"": [""low"", ""medium"", ""high"", ""critical""]
    },
    ""triggers"": {
      ""type"": ""array"",
      ""minItems"": 1,
      ""items"": {
        ""$ref"": ""#/definitions/trigger""
      }
    },
    ""conditions"": {
      ""type"": ""array"",
      ""items"": {
        ""$ref"": ""#/definitions/condition""
      }
    },
    ""aggregation"": {
      ""$ref"": ""#/definitions/aggregationWindow""
    },
    ""actions"": {
      ""type"": ""array"",
      ""items"": {
        ""$ref"": ""#/definitions/action""
      }
    },
    ""metadata"": {
      ""type"": ""object""
    }
  },
  ""definitions"": {
    ""trigger"": {
      ""type"": ""object"",
      ""required"": [""type"", ""eventType""],
      ""properties"": {
        ""type"": {
          ""type"": ""string"",
          ""enum"": [""event"", ""time"", ""threshold""]
        },
        ""eventType"": {
          ""type"": ""string"",
          ""minLength"": 1
        },
        ""source"": {
          ""type"": ""string""
        },
        ""filters"": {
          ""type"": ""object""
        }
      }
    },
    ""condition"": {
      ""type"": ""object"",
      ""required"": [""field"", ""operator""],
      ""properties"": {
        ""field"": {
          ""type"": ""string"",
          ""minLength"": 1
        },
        ""operator"": {
          ""type"": ""string"",
          ""enum"": [
            ""equals"", ""not_equals"", ""contains"", ""not_contains"",
            ""starts_with"", ""ends_with"", ""greater_than"", ""greater_than_or_equal"",
            ""less_than"", ""less_than_or_equal"", ""in"", ""not_in"",
            ""regex"", ""exists"", ""not_exists""
          ]
        },
        ""value"": {},
        ""caseSensitive"": {
          ""type"": ""boolean"",
          ""default"": true
        },
        ""negate"": {
          ""type"": ""boolean"",
          ""default"": false
        }
      }
    },
    ""aggregationWindow"": {
      ""type"": ""object"",
      ""required"": [""type"", ""size"", ""unit""],
      ""properties"": {
        ""type"": {
          ""type"": ""string"",
          ""enum"": [""time_window"", ""count"", ""sum"", ""average"", ""min"", ""max""]
        },
        ""size"": {
          ""type"": ""integer"",
          ""minimum"": 1
        },
        ""unit"": {
          ""type"": ""string"",
          ""enum"": [""seconds"", ""minutes"", ""hours"", ""days""]
        },
        ""groupBy"": {
          ""type"": ""array"",
          ""items"": {
            ""type"": ""string""
          }
        },
        ""having"": {
          ""$ref"": ""#/definitions/condition""
        }
      }
    },
    ""action"": {
      ""type"": ""object"",
      ""required"": [""type""],
      ""properties"": {
        ""type"": {
          ""type"": ""string"",
          ""enum"": [""alert"", ""webhook"", ""email"", ""script"", ""log"", ""block"", ""quarantine""]
        },
        ""parameters"": {
          ""type"": ""object""
        },
        ""delay"": {
          ""type"": ""integer"",
          ""minimum"": 0
        },
        ""retry"": {
          ""$ref"": ""#/definitions/retryPolicy""
        }
      }
    },
    ""retryPolicy"": {
      ""type"": ""object"",
      ""properties"": {
        ""attempts"": {
          ""type"": ""integer"",
          ""minimum"": 1,
          ""maximum"": 10,
          ""default"": 3
        },
        ""delay"": {
          ""type"": ""integer"",
          ""minimum"": 0,
          ""default"": 1000
        },
        ""backoff"": {
          ""type"": ""string"",
          ""enum"": [""fixed"", ""exponential"", ""linear""],
          ""default"": ""fixed""
        }
      }
    }
  }
}";
        return JsonSchema.FromText(schemaJson);
    }

    private List<string> ValidateTrigger(Trigger trigger)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(trigger.EventType))
        {
            errors.Add("Trigger event type is required");
        }

        if (trigger.Type == TriggerType.Threshold && trigger.Filters == null)
        {
            errors.Add("Threshold triggers require filters");
        }

        return errors;
    }

    private List<string> ValidateCondition(Condition condition)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(condition.Field))
        {
            errors.Add("Condition field is required");
        }

        // Check if value is required for this operator
        var valueRequired = condition.Operator != ConditionOperator.Exists && 
                          condition.Operator != ConditionOperator.NotExists;
        
        if (valueRequired && condition.Value == null)
        {
            errors.Add($"Condition value is required for operator '{condition.Operator}'");
        }

        // Validate regex patterns
        if (condition.Operator == ConditionOperator.Regex && condition.Value is string pattern)
        {
            try
            {
                System.Text.RegularExpressions.Regex.IsMatch("", pattern);
            }
            catch (ArgumentException)
            {
                errors.Add($"Invalid regex pattern: {pattern}");
            }
        }

        return errors;
    }

    private List<string> ValidateAggregation(AggregationWindow aggregation)
    {
        var errors = new List<string>();

        if (aggregation.Size <= 0)
        {
            errors.Add("Aggregation size must be greater than 0");
        }

        if (aggregation.GroupBy != null && aggregation.GroupBy.Count == 0)
        {
            errors.Add("GroupBy cannot be empty when specified");
        }

        return errors;
    }

    private List<string> ValidateAction(Models.Action action)
    {
        var errors = new List<string>();

        if (action.Type == ActionType.Webhook && 
            (action.Parameters == null || !action.Parameters.ContainsKey("url")))
        {
            errors.Add("Webhook actions require a 'url' parameter");
        }

        if (action.Type == ActionType.Email &&
            (action.Parameters == null || !action.Parameters.ContainsKey("recipients")))
        {
            errors.Add("Email actions require a 'recipients' parameter");
        }

        if (action.Retry != null)
        {
            if (action.Retry.Attempts <= 0)
            {
                errors.Add("Retry attempts must be greater than 0");
            }
            if (action.Retry.Delay < 0)
            {
                errors.Add("Retry delay cannot be negative");
            }
        }

        return errors;
    }
}

public record ValidationResult(bool IsValid, string Message, string? PropertyPath = null);