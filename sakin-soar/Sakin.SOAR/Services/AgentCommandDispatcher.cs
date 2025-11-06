using Microsoft.Extensions.Logging;
using Sakin.Common.Configuration;
using Sakin.Common.Models.SOAR;
using Sakin.Messaging.Producer;
using System.Text.Json;

namespace Sakin.SOAR.Services;

public interface IAgentCommandDispatcher
{
    Task<bool> DispatchCommandAsync(
        Guid correlationId,
        string targetAgentId,
        AgentCommandType command,
        string payload,
        CancellationToken cancellationToken = default);
}

public class AgentCommandDispatcher : IAgentCommandDispatcher
{
    private readonly IKafkaProducer _kafkaProducer;
    private readonly SoarKafkaTopics _kafkaTopics;
    private readonly ILogger<AgentCommandDispatcher> _logger;

    public AgentCommandDispatcher(
        IKafkaProducer kafkaProducer,
        Microsoft.Extensions.Options.IOptions<SoarKafkaTopics> kafkaTopicsOptions,
        ILogger<AgentCommandDispatcher> logger)
    {
        _kafkaProducer = kafkaProducer;
        _kafkaTopics = kafkaTopicsOptions.Value;
        _logger = logger;
    }

    public async Task<bool> DispatchCommandAsync(
        Guid correlationId,
        string targetAgentId,
        AgentCommandType command,
        string payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var commandRequest = new AgentCommandRequest(
                correlationId,
                targetAgentId,
                command,
                payload,
                DateTime.UtcNow.AddMinutes(5)); // 5 minute expiry

            var messageJson = JsonSerializer.Serialize(commandRequest);

            await _kafkaProducer.ProduceAsync(
                _kafkaTopics.AgentCommand,
                correlationId.ToString(),
                messageJson,
                cancellationToken);

            _logger.LogInformation(
                "Dispatched agent command {Command} to agent {AgentId} (CorrelationId: {CorrelationId})",
                command,
                targetAgentId,
                correlationId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to dispatch agent command {Command} to agent {AgentId} (CorrelationId: {CorrelationId})",
                command,
                targetAgentId,
                correlationId);
            return false;
        }
    }
}