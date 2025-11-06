using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sakin.Common.Audit;
using Sakin.Common.Models.SOAR;
using Sakin.Messaging.Consumer;
using Sakin.SOAR.Services;
using System.Text.Json;

namespace Sakin.SOAR.Workers;

public class SoarWorker : BackgroundService
{
    private readonly IKafkaConsumer _kafkaConsumer;
    private readonly IPlaybookExecutor _playbookExecutor;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<SoarWorker> _logger;

    public SoarWorker(
        IKafkaConsumer kafkaConsumer,
        IPlaybookExecutor playbookExecutor,
        IAuditLogger auditLogger,
        ILogger<SoarWorker> logger)
    {
        _kafkaConsumer = kafkaConsumer;
        _playbookExecutor = playbookExecutor;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SOAR Worker starting up...");

        try
        {
            await _kafkaConsumer.SubscribeAsync(stoppingToken);
            _logger.LogInformation("Subscribed to alert actions topic");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = await _kafkaConsumer.ConsumeAsync(stoppingToken);
                    
                    if (consumeResult != null)
                    {
                        await ProcessAlertAction(consumeResult.Message.Value, stoppingToken);
                        
                        // Only commit offset after successful processing
                        await _kafkaConsumer.CommitAsync(consumeResult, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("SOAR Worker stopping...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    // Continue processing other messages
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SOAR Worker failed to start");
            throw;
        }
        finally
        {
            _kafkaConsumer?.Dispose();
        }
    }

    private async Task ProcessAlertAction(string messageJson, CancellationToken cancellationToken)
    {
        AlertActionMessage? alertActionMessage = null;
        var auditCorrelation = Guid.NewGuid();

        try
        {
            _logger.LogDebug("Processing alert action message: {Message}", messageJson);

            alertActionMessage = JsonSerializer.Deserialize<AlertActionMessage>(messageJson);
            if (alertActionMessage == null)
            {
                _logger.LogWarning("Failed to deserialize alert action message");
                return;
            }

            _logger.LogInformation(
                "Processing alert action for alert {AlertId} from rule {RuleId}",
                alertActionMessage.Alert.Id,
                alertActionMessage.Rule.Id);

            // Find playbook actions in the rule
            var playbookActions = alertActionMessage.Rule.Actions
                .Where(a => a.Type == ActionType.Playbook)
                .ToList();

            if (!playbookActions.Any())
            {
                _logger.LogDebug("No playbook actions found for alert {AlertId}", alertActionMessage.Alert.Id);
                return;
            }

            // Execute each playbook action
            foreach (var action in playbookActions)
            {
                if (action.Parameters?.TryGetValue("playbook_id", out var playbookIdObj) == true &&
                    playbookIdObj?.ToString() is { } playbookId)
                {
                    await _playbookExecutor.ExecutePlaybookAsync(
                        playbookId,
                        alertActionMessage.Alert,
                        action.Parameters,
                        cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Playbook action missing playbook_id parameter for alert {AlertId}", 
                        alertActionMessage.Alert.Id);
                }
            }

            await _auditLogger.LogAuditEventAsync(
                alertActionMessage.Alert.Source ?? "system",
                "soar.alert_action.processed",
                auditCorrelation,
                new
                {
                    AlertId = alertActionMessage.Alert.Id,
                    RuleId = alertActionMessage.Rule.Id,
                    PlaybookCount = playbookActions.Count,
                    ProcessedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process alert action message: {Message}", messageJson);
            
            await _auditLogger.LogAuditEventAsync(
                alertActionMessage?.Alert.Source ?? "system",
                "soar.alert_action.failed",
                auditCorrelation,
                new
                {
                    Message = messageJson,
                    Error = ex.Message,
                    FailedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SOAR Worker stopping...");
        await base.StopAsync(cancellationToken);
    }
}