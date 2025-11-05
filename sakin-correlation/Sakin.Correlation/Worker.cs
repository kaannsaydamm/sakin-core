using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Models;
using Sakin.Correlation.Configuration;
using Sakin.Messaging.Consumer;

namespace Sakin.Correlation;

public class Worker : BackgroundService
{
    private readonly IKafkaConsumer _consumer;
    private readonly ILogger<Worker> _logger;
    private readonly KafkaWorkerOptions _options;

    public Worker(
        IKafkaConsumer consumer,
        IOptions<KafkaWorkerOptions> options,
        ILogger<Worker> logger)
    {
        _consumer = consumer;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Topic))
        {
            _logger.LogWarning("Kafka topic is not configured. The correlation worker will not consume messages.");
            return;
        }

        _logger.LogInformation(
            "Correlation worker started. Listening topic {Topic} with consumer group {ConsumerGroup}.",
            _options.Topic,
            _options.ConsumerGroup);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _consumer.ConsumeAsync<NormalizedEvent>(result =>
                {
                    if (result.Message is null)
                    {
                        _logger.LogWarning(
                            "Received empty message from topic {Topic} at offset {Offset}.",
                            result.Topic,
                            result.Offset);
                        return Task.CompletedTask;
                    }

                    _logger.LogInformation(
                        "Consumed normalized event {@Event} from topic {Topic}, partition {Partition}, offset {Offset}.",
                        result.Message,
                        result.Topic,
                        result.Partition,
                        result.Offset);

                    return Task.CompletedTask;
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Correlation worker cancellation requested.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while consuming Kafka messages. Retrying...");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping correlation worker.");
        _consumer.Unsubscribe();
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
