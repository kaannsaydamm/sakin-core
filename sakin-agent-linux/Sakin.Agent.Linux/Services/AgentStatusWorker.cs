using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Agent.Linux.Configuration;
using Sakin.Messaging.Producer;
using System.Text.Json;

namespace Sakin.Agent.Linux.Services;

public class AgentStatusWorker : BackgroundService
{
    private readonly IKafkaProducer _kafkaProducer;
    private readonly AgentOptions _agentOptions;
    private readonly SoarKafkaTopics _kafkaTopics;
    private readonly ILogger<AgentStatusWorker> _logger;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromMinutes(5);

    public AgentStatusWorker(
        IKafkaProducer kafkaProducer,
        IOptions<AgentOptions> agentOptions,
        IOptions<SoarKafkaTopics> kafkaTopicsOptions,
        ILogger<AgentStatusWorker> logger)
    {
        _kafkaProducer = kafkaProducer;
        _agentOptions = agentOptions.Value;
        _kafkaTopics = kafkaTopicsOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent Status Worker starting up for agent: {AgentId}", _agentOptions.AgentId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishHeartbeatAsync(stoppingToken);
                await Task.Delay(_heartbeatInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Agent Status Worker stopping...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing heartbeat");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task PublishHeartbeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            var heartbeat = new
            {
                Type = "agent_heartbeat",
                AgentId = _agentOptions.AgentId,
                Hostname = Environment.MachineName,
                OperatingSystem = "Linux",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0"
            };

            var heartbeatJson = JsonSerializer.Serialize(heartbeat);

            await _kafkaProducer.ProduceAsync(
                _kafkaTopics.AuditLog,
                $"heartbeat-{_agentOptions.AgentId}",
                heartbeatJson,
                cancellationToken);

            _logger.LogDebug("Published heartbeat for agent {AgentId}", _agentOptions.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish heartbeat for agent {AgentId}", _agentOptions.AgentId);
        }
    }
}