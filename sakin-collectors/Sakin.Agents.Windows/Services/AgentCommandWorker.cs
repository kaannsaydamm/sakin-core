using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Agents.Windows.Configuration;
using Sakin.Agents.Windows.Services;
using Sakin.Common.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Serialization;
using System.Text.Json;

namespace Sakin.Agents.Windows.Services;

public class AgentCommandWorker : BackgroundService
{
    private readonly IKafkaConsumer _kafkaConsumer;
    private readonly IAgentCommandHandler _commandHandler;
    private readonly AgentOptions _agentOptions;
    private readonly SoarKafkaTopics _kafkaTopics;
    private readonly ILogger<AgentCommandWorker> _logger;

    public AgentCommandWorker(
        IKafkaConsumer kafkaConsumer,
        IAgentCommandHandler commandHandler,
        IOptions<AgentOptions> agentOptions,
        IOptions<SoarKafkaTopics> kafkaTopicsOptions,
        ILogger<AgentCommandWorker> logger)
    {
        _kafkaConsumer = kafkaConsumer;
        _commandHandler = commandHandler;
        _agentOptions = agentOptions.Value;
        _kafkaTopics = kafkaTopicsOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent Command Worker starting up for agent: {AgentId}", _agentOptions.AgentId);

        try
        {
            await _kafkaConsumer.SubscribeAsync(stoppingToken);
            _logger.LogInformation("Subscribed to agent command topic: {Topic}", _kafkaTopics.AgentCommand);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = await _kafkaConsumer.ConsumeAsync(stoppingToken);
                    
                    if (consumeResult != null)
                    {
                        await ProcessCommandMessage(consumeResult.Message.Value, stoppingToken);
                        
                        // Only commit offset after successful processing
                        await _kafkaConsumer.CommitAsync(consumeResult, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Agent Command Worker stopping...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing agent command message");
                    // Continue processing other messages
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent Command Worker failed to start");
            throw;
        }
        finally
        {
            _kafkaConsumer?.Dispose();
        }
    }

    private async Task ProcessCommandMessage(string messageJson, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Processing agent command message: {Message}", messageJson);

            var commandRequest = JsonSerializer.Deserialize<AgentCommandRequest>(messageJson);
            if (commandRequest == null)
            {
                _logger.LogWarning("Failed to deserialize agent command message");
                return;
            }

            // Filter commands for this agent
            if (commandRequest.TargetAgentId != _agentOptions.AgentId)
            {
                _logger.LogDebug(
                    "Ignoring command for different agent. Expected: {ExpectedAgentId}, Got: {TargetAgentId}",
                    _agentOptions.AgentId,
                    commandRequest.TargetAgentId);
                return;
            }

            _logger.LogInformation(
                "Processing command {Command} for agent {AgentId} (CorrelationId: {CorrelationId})",
                commandRequest.Command,
                commandRequest.TargetAgentId,
                commandRequest.CorrelationId);

            // Handle the command
            var result = await _commandHandler.HandleCommandAsync(commandRequest, cancellationToken);

            _logger.LogInformation(
                "Command processing completed. Success: {Success} (CorrelationId: {CorrelationId})",
                result.Success,
                result.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process agent command message: {Message}", messageJson);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Agent Command Worker stopping...");
        await base.StopAsync(cancellationToken);
    }
}