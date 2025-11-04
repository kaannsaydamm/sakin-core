using Microsoft.Extensions.Logging;
using Sakin.Correlation.Exceptions;
using Sakin.Correlation.Models;
using Sakin.Correlation.Validation;
using System.Text.Json;

namespace Sakin.Correlation.Parsers;

public interface IRuleParser
{
    Task<CorrelationRule> ParseRuleAsync(string ruleJson);
    Task<CorrelationRule> ParseRuleFromFileAsync(string filePath);
    Task<List<CorrelationRule>> ParseRulesFromDirectoryAsync(string directoryPath);
    string SerializeRule(CorrelationRule rule);
}

public class RuleParser : IRuleParser
{
    private readonly IRuleValidator _validator;
    private readonly ILogger<RuleParser> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RuleParser(IRuleValidator validator, ILogger<RuleParser> logger)
    {
        _validator = validator;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    public async Task<CorrelationRule> ParseRuleAsync(string ruleJson)
    {
        try
        {
            _logger.LogDebug("Starting rule parsing");

            // Validate the rule first
            var validationResult = await _validator.ValidateRuleAsync(ruleJson);
            if (!validationResult.IsValid)
            {
                throw new RuleParsingException(
                    validationResult.PropertyPath ?? "unknown",
                    validationResult.Message
                );
            }

            // Parse the rule
            var rule = JsonSerializer.Deserialize<CorrelationRule>(ruleJson, _jsonOptions);
            if (rule == null)
            {
                throw new RuleParsingException("Failed to deserialize rule after validation");
            }

            // Additional post-validation checks
            await PostValidateRule(rule);

            _logger.LogInformation("Successfully parsed rule: {RuleId}", rule.Id);
            return rule;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error");
            throw new RuleParsingException("root", "root", $"JSON parsing error: {ex.Message}", ex);
        }
        catch (RuleParsingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected parsing error");
            throw new RuleParsingException("root", "root", $"Unexpected error: {ex.Message}", ex);
        }
    }

    public async Task<CorrelationRule> ParseRuleFromFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new RuleParsingException($"File not found: {filePath}");
            }

            var ruleJson = await File.ReadAllTextAsync(filePath);
            var rule = await ParseRuleAsync(ruleJson);

            // Validate that the file name matches the rule ID (optional but good practice)
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (!string.Equals(fileName, rule.Id, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "File name '{FileName}' does not match rule ID '{RuleId}'", 
                    fileName, rule.Id);
            }

            return rule;
        }
        catch (RuleParsingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing rule from file: {FilePath}", filePath);
            throw new RuleParsingException($"file:{filePath}", "file", $"Error reading file: {ex.Message}", ex);
        }
    }

    public async Task<List<CorrelationRule>> ParseRulesFromDirectoryAsync(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new RuleParsingException($"Directory not found: {directoryPath}");
            }

            var ruleFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
            var rules = new List<CorrelationRule>();
            var errors = new List<string>();

            foreach (var filePath in ruleFiles)
            {
                try
                {
                    var rule = await ParseRuleFromFileAsync(filePath);
                    rules.Add(rule);
                }
                catch (RuleParsingException ex)
                {
                    var error = $"Failed to parse {Path.GetFileName(filePath)}: {ex.Message}";
                    errors.Add(error);
                    _logger.LogWarning(error);
                }
            }

            if (rules.Count == 0 && errors.Count > 0)
            {
                throw new RuleParsingException($"No valid rules found in directory. Errors: {string.Join("; ", errors)}");
            }

            if (errors.Count > 0)
            {
                _logger.LogWarning("Completed parsing with {ErrorCount} errors", errors.Count);
            }

            _logger.LogInformation("Successfully parsed {RuleCount} rules from directory: {DirectoryPath}", 
                rules.Count, directoryPath);

            return rules;
        }
        catch (RuleParsingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing rules from directory: {DirectoryPath}", directoryPath);
            throw new RuleParsingException($"directory:{directoryPath}", "directory", $"Error reading directory: {ex.Message}", ex);
        }
    }

    public string SerializeRule(CorrelationRule rule)
    {
        try
        {
            return JsonSerializer.Serialize(rule, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serializing rule: {RuleId}", rule.Id);
            throw new RuleParsingException(rule.Id, "serialization", $"Serialization error: {ex.Message}", ex);
        }
    }

    private async Task PostValidateRule(CorrelationRule rule)
    {
        // Check for duplicate trigger event types
        var duplicateEventTypes = rule.Triggers
            .GroupBy(t => t.EventType)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateEventTypes.Count > 0)
        {
            throw new RuleParsingException(
                rule.Id, 
                "triggers", 
                $"Duplicate event types found: {string.Join(", ", duplicateEventTypes)}"
            );
        }

        // Validate that aggregation makes sense with the conditions
        if (rule.Aggregation != null && rule.Conditions.Count == 0)
        {
            _logger.LogWarning("Rule {RuleId} has aggregation but no conditions", rule.Id);
        }

        // Check for logical consistency
        if (rule.Triggers.Any(t => t.Type == TriggerType.Time) && rule.Aggregation == null)
        {
            _logger.LogWarning("Rule {RuleId} has time trigger but no aggregation window", rule.Id);
        }

        await Task.CompletedTask;
    }
}