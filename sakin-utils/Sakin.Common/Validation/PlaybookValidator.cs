using Microsoft.Extensions.Logging;
using Sakin.Common.Models.SOAR;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Sakin.Common.Validation;

public interface IPlaybookValidator
{
    ValidationResult ValidatePlaybook(string yamlContent);
    ValidationResult ValidatePlaybookFile(string filePath);
    PlaybookDefinition? ParsePlaybook(string yamlContent);
}

public class PlaybookValidator : IPlaybookValidator
{
    private readonly ILogger<PlaybookValidator> _logger;
    private readonly IDeserializer _deserializer;

    public PlaybookValidator(ILogger<PlaybookValidator> logger)
    {
        _logger = logger;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public ValidationResult ValidatePlaybook(string yamlContent)
    {
        try
        {
            var playbook = ParsePlaybook(yamlContent);
            if (playbook == null)
            {
                return ValidationResult.Failure("Failed to parse playbook YAML");
            }

            return ValidatePlaybookDefinition(playbook);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate playbook YAML");
            return ValidationResult.Failure($"YAML parsing error: {ex.Message}");
        }
    }

    public ValidationResult ValidatePlaybookFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return ValidationResult.Failure($"Playbook file not found: {filePath}");
            }

            var yamlContent = File.ReadAllText(filePath);
            return ValidatePlaybook(yamlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate playbook file: {FilePath}", filePath);
            return ValidationResult.Failure($"File validation error: {ex.Message}");
        }
    }

    public PlaybookDefinition? ParsePlaybook(string yamlContent)
    {
        try
        {
            return _deserializer.Deserialize<PlaybookDefinition>(yamlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize playbook YAML");
            return null;
        }
    }

    private ValidationResult ValidatePlaybookDefinition(PlaybookDefinition playbook)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(playbook.Id))
            errors.Add("Playbook ID is required");

        if (string.IsNullOrWhiteSpace(playbook.Name))
            errors.Add("Playbook name is required");

        if (playbook.Steps == null || !playbook.Steps.Any())
            errors.Add("Playbook must have at least one step");
        else
        {
            for (int i = 0; i < playbook.Steps.Count; i++)
            {
                var stepErrors = ValidateStep(playbook.Steps[i], i);
                errors.AddRange(stepErrors);
            }
        }

        return errors.Any() 
            ? ValidationResult.Failure(string.Join("; ", errors))
            : ValidationResult.Success();
    }

    private List<string> ValidateStep(PlaybookStep step, int stepIndex)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(step.Id))
            errors.Add($"Step {stepIndex + 1}: Step ID is required");

        if (string.IsNullOrWhiteSpace(step.Action))
            errors.Add($"Step {stepIndex + 1}: Step action is required");

        if (step.Parameters == null || !step.Parameters.Any())
            errors.Add($"Step {stepIndex + 1}: Step parameters are required");

        // Validate specific actions
        if (!string.IsNullOrWhiteSpace(step.Action))
        {
            switch (step.Action.ToLowerInvariant())
            {
                case "notify_slack":
                    ValidateSlackStep(step, stepIndex, errors);
                    break;
                case "create_jira_ticket":
                    ValidateJiraStep(step, stepIndex, errors);
                    break;
                case "send_email":
                    ValidateEmailStep(step, stepIndex, errors);
                    break;
                case "dispatch_agent_command":
                    ValidateAgentCommandStep(step, stepIndex, errors);
                    break;
            }
        }

        return errors;
    }

    private void ValidateSlackStep(PlaybookStep step, int stepIndex, List<string> errors)
    {
        if (!step.Parameters.ContainsKey("channel") && !step.Parameters.ContainsKey("message"))
            errors.Add($"Step {stepIndex + 1}: Slack notification requires 'channel' and 'message' parameters");
    }

    private void ValidateJiraStep(PlaybookStep step, int stepIndex, List<string> errors)
    {
        if (!step.Parameters.ContainsKey("summary") || !step.Parameters.ContainsKey("description"))
            errors.Add($"Step {stepIndex + 1}: Jira ticket creation requires 'summary' and 'description' parameters");
    }

    private void ValidateEmailStep(PlaybookStep step, int stepIndex, List<string> errors)
    {
        if (!step.Parameters.ContainsKey("to") || !step.Parameters.ContainsKey("subject") || !step.Parameters.ContainsKey("body"))
            errors.Add($"Step {stepIndex + 1}: Email notification requires 'to', 'subject', and 'body' parameters");
    }

    private void ValidateAgentCommandStep(PlaybookStep step, int stepIndex, List<string> errors)
    {
        if (!step.Parameters.ContainsKey("target_agent_id") || !step.Parameters.ContainsKey("command") || !step.Parameters.ContainsKey("payload"))
            errors.Add($"Step {stepIndex + 1}: Agent command requires 'target_agent_id', 'command', and 'payload' parameters");

        if (step.Parameters.ContainsKey("command"))
        {
            var commandStr = step.Parameters["command"]?.ToString();
            if (!Enum.TryParse<AgentCommandType>(commandStr, true, out _))
                errors.Add($"Step {stepIndex + 1}: Invalid command type '{commandStr}'. Valid types: {string.Join(", ", Enum.GetNames<AgentCommandType>())}");
        }
    }
}

public record ValidationResult(bool IsValid, string? ErrorMessage = null)
{
    public static ValidationResult Success() => new(true);
    public static ValidationResult Failure(string errorMessage) => new(false, errorMessage);
}