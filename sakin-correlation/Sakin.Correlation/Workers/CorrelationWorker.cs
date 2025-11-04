using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Models;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Services;
using Sakin.Messaging.Consumer;

namespace Sakin.Correlation.Workers;

public class CorrelationWorker : BackgroundService
{
    private readonly IKafkaConsumer _consumer;
    private readonly CorrelationPipeline _pipeline;
    private readonly CorrelationKafkaOptions _options;
    private readonly ILogger<CorrelationWorker> _logger;

    public CorrelationWorker(
        IKafkaConsumer consumer,
        CorrelationPipeline pipeline,
        IOptions<CorrelationKafkaOptions> options,
        ILogger<CorrelationWorker> logger)
    {
        _consumer = consumer;
        _pipeline = pipeline;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var topic = string.IsNullOrWhiteSpace(_options.NormalizedEventsTopic)
            ? "normalized-events"
            : _options.NormalizedEventsTopic;

        _logger.LogInformation(
            "Starting correlation worker consuming from topic {Topic}, consumer group {ConsumerGroup}",
            topic,
            _options.ConsumerGroup);

        try
        {
            await _consumer.ConsumeAsync<EventEnvelope>(async result =>
            {
                if (result.Message is null)
                {
                    _logger.LogWarning("Received null message on topic {Topic}", result.Topic);
                    return;
                }

                _logger.LogDebug(
                    "Received event {EventId} from topic {Topic}, partition {Partition}, offset {Offset}",
                    result.Message.EventId,
                    result.Topic,
                    result.Partition,
                    result.Offset);

                await _pipeline.EnqueueAsync(result, stoppingToken);
            }, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Correlation worker cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in correlation worker");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping correlation worker");

        try
        {
            await _pipeline.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while disposing pipeline during shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}
