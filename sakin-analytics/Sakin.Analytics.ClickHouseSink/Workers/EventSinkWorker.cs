using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Analytics.ClickHouseSink.Services;
using Sakin.Common.Configuration;
using Sakin.Common.Models;
using Sakin.Messaging.Kafka;

namespace Sakin.Analytics.ClickHouseSink.Workers;

public class EventSinkWorker : BackgroundService
{
    private readonly ILogger<EventSinkWorker> _logger;
    private readonly IKafkaConsumer<EventEnvelope> _kafkaConsumer;
    private readonly IClickHouseService _clickHouseService;
    private readonly BaselineAggregationOptions _options;
    private readonly Channel<EventEnvelope> _eventChannel;
    private readonly CancellationTokenSource _flushCts;

    public EventSinkWorker(
        ILogger<EventSinkWorker> logger,
        IKafkaConsumer<EventEnvelope> kafkaConsumer,
        IClickHouseService clickHouseService,
        IOptions<BaselineAggregationOptions> options)
    {
        _logger = logger;
        _kafkaConsumer = kafkaConsumer;
        _clickHouseService = clickHouseService;
        _options = options.Value;
        _eventChannel = Channel.CreateUnbounded<EventEnvelope>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _flushCts = new CancellationTokenSource();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("ClickHouse sink is disabled in configuration");
            return;
        }

        _logger.LogInformation("EventSinkWorker starting...");

        await _clickHouseService.InitializeAsync(stoppingToken);

        var consumerTask = ConsumeFromKafkaAsync(stoppingToken);
        var batchProcessorTask = BatchProcessEventsAsync(stoppingToken);

        await Task.WhenAll(consumerTask, batchProcessorTask);
    }

    private async Task ConsumeFromKafkaAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Kafka consumer for topic: {Topic}", _options.KafkaTopic);

        await foreach (var message in _kafkaConsumer.ConsumeAsync(_options.KafkaTopic, stoppingToken))
        {
            try
            {
                if (message.Value != null)
                {
                    await _eventChannel.Writer.WriteAsync(message.Value, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Kafka message");
            }
        }
    }

    private async Task BatchProcessEventsAsync(CancellationToken stoppingToken)
    {
        var batch = new List<EventEnvelope>();
        var batchTimer = new PeriodicTimer(TimeSpan.FromSeconds(_options.BatchTimeoutSeconds));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var hasEvent = await _eventChannel.Reader.WaitToReadAsync(stoppingToken);
                
                while (batch.Count < _options.BatchSize && _eventChannel.Reader.TryRead(out var evt))
                {
                    batch.Add(evt);
                }

                var shouldFlush = batch.Count >= _options.BatchSize;
                
                if (!shouldFlush)
                {
                    shouldFlush = await batchTimer.WaitForNextTickAsync(stoppingToken);
                }

                if (shouldFlush && batch.Count > 0)
                {
                    await FlushBatchAsync(batch, stoppingToken);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Batch processor is shutting down...");
        }
        finally
        {
            if (batch.Count > 0)
            {
                await FlushBatchAsync(batch, CancellationToken.None);
            }
            batchTimer.Dispose();
        }
    }

    private async Task FlushBatchAsync(List<EventEnvelope> batch, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Flushing batch of {EventCount} events to ClickHouse", batch.Count);
            await _clickHouseService.BatchInsertEventsAsync(batch, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush batch of {EventCount} events", batch.Count);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("EventSinkWorker stopping...");
        _eventChannel.Writer.Complete();
        await base.StopAsync(cancellationToken);
    }
}
