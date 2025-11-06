using Microsoft.Extensions.Logging;
using Sakin.Common.Models.SOAR;
using Sakin.Correlation.Models;
using System.Text.Json;

namespace Sakin.SOAR.Services;

public interface IPlaybookExecutor
{
    Task<PlaybookExecutionResult> ExecutePlaybookAsync(
        string playbookId,
        AlertEntity alert,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
}

public class PlaybookExecutor : IPlaybookExecutor
{
    private readonly IPlaybookRepository _playbookRepository;
    private readonly INotificationService _notificationService;
    private readonly IAgentCommandDispatcher _agentCommandDispatcher;
    private readonly IAuditService _auditService;
    private readonly ILogger<PlaybookExecutor> _logger;

    public PlaybookExecutor(
        IPlaybookRepository playbookRepository,
        INotificationService notificationService,
        IAgentCommandDispatcher agentCommandDispatcher,
        IAuditService auditService,
        ILogger<PlaybookExecutor> logger)
    {
        _playbookRepository = playbookRepository;
        _notificationService = notificationService;
        _agentCommandDispatcher = agentCommandDispatcher;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<PlaybookExecutionResult> ExecutePlaybookAsync(
        string playbookId,
        AlertEntity alert,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;
        var stepResults = new List<StepExecutionResult>();

        _logger.LogInformation(
            "Starting playbook execution: {PlaybookId} for alert {AlertId} (ExecutionId: {ExecutionId})",
            playbookId,
            alert.Id,
            executionId);

        try
        {
            var playbook = await _playbookRepository.GetPlaybookAsync(playbookId, cancellationToken);
            if (playbook == null)
            {
                var error = $"Playbook not found: {playbookId}";
                _logger.LogError(error);
                return new PlaybookExecutionResult(executionId, playbookId, false, stepResults, error, startedAt, DateTime.UtcNow);
            }

            if (!playbook.Enabled)
            {
                var error = $"Playbook is disabled: {playbookId}";
                _logger.LogWarning(error);
                return new PlaybookExecutionResult(executionId, playbookId, false, stepResults, error, startedAt, DateTime.UtcNow);
            }

            // Execute each step
            foreach (var step in playbook.Steps)
            {
                var stepResult = await ExecuteStepAsync(step, alert, parameters, cancellationToken);
                stepResults.Add(stepResult);

                // If step failed and no retry policy, stop execution
                if (!stepResult.Success && step.RetryCount == null)
                {
                    _logger.LogWarning("Step {StepId} failed, stopping playbook execution", step.Id);
                    break;
                }
            }

            var success = stepResults.All(r => r.Success);
            var completedAt = DateTime.UtcNow;

            await _auditService.WriteAuditEventAsync(new
            {
                Type = "playbook_execution_completed",
                ExecutionId = executionId,
                PlaybookId = playbookId,
                AlertId = alert.Id,
                Success = success,
                StepCount = stepResults.Count,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Duration = (completedAt - startedAt).TotalMilliseconds
            }, cancellationToken);

            _logger.LogInformation(
                "Playbook execution completed: {PlaybookId} for alert {AlertId} (ExecutionId: {ExecutionId}) - Success: {Success}",
                playbookId,
                alert.Id,
                executionId,
                success);

            return new PlaybookExecutionResult(executionId, playbookId, success, stepResults, null, startedAt, completedAt);
        }
        catch (Exception ex)
        {
            var completedAt = DateTime.UtcNow;
            var error = $"Playbook execution failed: {ex.Message}";
            
            _logger.LogError(ex, "Playbook execution failed: {PlaybookId} for alert {AlertId}", playbookId, alert.Id);

            await _auditService.WriteAuditEventAsync(new
            {
                Type = "playbook_execution_failed",
                ExecutionId = executionId,
                PlaybookId = playbookId,
                AlertId = alert.Id,
                Error = ex.Message,
                StackTrace = ex.StackTrace,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Duration = (completedAt - startedAt).TotalMilliseconds
            }, cancellationToken);

            return new PlaybookExecutionResult(executionId, playbookId, false, stepResults, error, startedAt, completedAt);
        }
    }

    private async Task<StepExecutionResult> ExecuteStepAsync(
        PlaybookStep step,
        AlertEntity alert,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        var stepStartedAt = DateTime.UtcNow;
        _logger.LogDebug("Executing step {StepId}: {Action}", step.Id, step.Action);

        try
        {
            // Check condition if specified
            if (!string.IsNullOrWhiteSpace(step.Condition))
            {
                if (!EvaluateCondition(step.Condition, alert, parameters))
                {
                    _logger.LogDebug("Step {StepId} condition not met, skipping", step.Id);
                    return new StepExecutionResult(step.Id, true, "Condition not met, step skipped", null, stepStartedAt, DateTime.UtcNow);
                }
            }

            var result = await ExecuteStepActionAsync(step, alert, parameters, cancellationToken);
            var completedAt = DateTime.UtcNow;

            await _auditService.WriteAuditEventAsync(new
            {
                Type = "step_execution_completed",
                StepId = step.Id,
                Action = step.Action,
                Success = result.Success,
                Output = result.Output,
                Error = result.ErrorMessage,
                Duration = (completedAt - stepStartedAt).TotalMilliseconds
            }, cancellationToken);

            return new StepExecutionResult(step.Id, result.Success, result.Output, result.ErrorMessage, stepStartedAt, completedAt);
        }
        catch (Exception ex)
        {
            var completedAt = DateTime.UtcNow;
            var error = $"Step execution failed: {ex.Message}";
            
            _logger.LogError(ex, "Step {StepId} execution failed", step.Id);

            await _auditService.WriteAuditEventAsync(new
            {
                Type = "step_execution_failed",
                StepId = step.Id,
                Action = step.Action,
                Error = ex.Message,
                StackTrace = ex.StackTrace,
                Duration = (completedAt - stepStartedAt).TotalMilliseconds
            }, cancellationToken);

            return new StepExecutionResult(step.Id, false, null, error, stepStartedAt, completedAt);
        }
    }

    private async Task<(bool Success, string? Output, string? ErrorMessage)> ExecuteStepActionAsync(
        PlaybookStep step,
        AlertEntity alert,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (step.Action.ToLowerInvariant())
            {
                case "notify_slack":
                    return await ExecuteSlackNotificationAsync(step, alert, parameters, cancellationToken);

                case "send_email":
                    return await ExecuteEmailNotificationAsync(step, alert, parameters, cancellationToken);

                case "create_jira_ticket":
                    return await ExecuteJiraTicketAsync(step, alert, parameters, cancellationToken);

                case "dispatch_agent_command":
                    return await ExecuteAgentCommandAsync(step, alert, parameters, cancellationToken);

                default:
                    return (false, null, $"Unknown action: {step.Action}");
            }
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    private async Task<(bool Success, string? Output, string? ErrorMessage)> ExecuteSlackNotificationAsync(
        PlaybookStep step,
        AlertEntity alert,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        var channel = step.Parameters.GetValueOrDefault("channel")?.ToString();
        var message = step.Parameters.GetValueOrDefault("message")?.ToString();

        if (string.IsNullOrWhiteSpace(message))
        {
            return (false, null, "Slack notification requires 'message' parameter");
        }

        var success = await _notificationService.SendSlackNotificationAsync(channel, message, cancellationToken);
        return (success, "Slack notification sent", success ? null : "Failed to send Slack notification");
    }

    private async Task<(bool Success, string? Output, string? ErrorMessage)> ExecuteEmailNotificationAsync(
        PlaybookStep step,
        AlertEntity alert,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        var to = step.Parameters.GetValueOrDefault("to")?.ToString();
        var subject = step.Parameters.GetValueOrDefault("subject")?.ToString();
        var body = step.Parameters.GetValueOrDefault("body")?.ToString();

        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            return (false, null, "Email notification requires 'to', 'subject', and 'body' parameters");
        }

        var recipients = to.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        var success = await _notificationService.SendEmailNotificationAsync(recipients, subject, body, cancellationToken);
        return (success, "Email notification sent", success ? null : "Failed to send email notification");
    }

    private async Task<(bool Success, string? Output, string? ErrorMessage)> ExecuteJiraTicketAsync(
        PlaybookStep step,
        AlertEntity alert,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        var summary = step.Parameters.GetValueOrDefault("summary")?.ToString();
        var description = step.Parameters.GetValueOrDefault("description")?.ToString();

        if (string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(description))
        {
            return (false, null, "Jira ticket creation requires 'summary' and 'description' parameters");
        }

        var ticketUrl = await _notificationService.CreateJiraTicketAsync(summary, description, cancellationToken);
        return (!string.IsNullOrWhiteSpace(ticketUrl), ticketUrl, string.IsNullOrWhiteSpace(ticketUrl) ? "Failed to create Jira ticket" : null);
    }

    private async Task<(bool Success, string? Output, string? ErrorMessage)> ExecuteAgentCommandAsync(
        PlaybookStep step,
        AlertEntity alert,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        var targetAgentId = step.Parameters.GetValueOrDefault("target_agent_id")?.ToString();
        var commandStr = step.Parameters.GetValueOrDefault("command")?.ToString();
        var payload = step.Parameters.GetValueOrDefault("payload")?.ToString();

        if (string.IsNullOrWhiteSpace(targetAgentId) || string.IsNullOrWhiteSpace(commandStr) || string.IsNullOrWhiteSpace(payload))
        {
            return (false, null, "Agent command requires 'target_agent_id', 'command', and 'payload' parameters");
        }

        if (!Enum.TryParse<AgentCommandType>(commandStr, true, out var commandType))
        {
            return (false, null, $"Invalid command type: {commandStr}");
        }

        var correlationId = Guid.NewGuid();
        var success = await _agentCommandDispatcher.DispatchCommandAsync(correlationId, targetAgentId, commandType, payload, cancellationToken);
        return (success, $"Agent command dispatched (CorrelationId: {correlationId})", success ? null : "Failed to dispatch agent command");
    }

    private bool EvaluateCondition(string condition, AlertEntity alert, Dictionary<string, object>? parameters)
    {
        // Simple condition evaluation for now
        // In a real implementation, you'd want a more sophisticated expression evaluator
        
        if (condition.Equals("high_severity", StringComparison.OrdinalIgnoreCase))
        {
            return alert.Severity.Equals("high", StringComparison.OrdinalIgnoreCase) || 
                   alert.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase);
        }

        if (condition.Equals("medium_or_higher", StringComparison.OrdinalIgnoreCase))
        {
            return alert.Severity.Equals("medium", StringComparison.OrdinalIgnoreCase) ||
                   alert.Severity.Equals("high", StringComparison.OrdinalIgnoreCase) ||
                   alert.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase);
        }

        // Default to true for unknown conditions
        return true;
    }
}