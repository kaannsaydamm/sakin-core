using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Models;
using Sakin.Correlation.Configuration;
using Sakin.Messaging.Consumer;

namespace Sakin.Correlation.Services;

public class CorrelationPipeline : IAsyncDisposable
{
    private readonly Channel<ConsumeResult<EventEnvelope>> _channel;
    private readonly IRuleEngine _ruleEngine;
    private readonly IAlertRepository _alertRepository;
    private readonly IAlertPublisher _alertPublisher;
    private readonly CorrelationPipelineOptions _options;
    private readonly ILogger<CorrelationPipeline> _logger;
    private readonly List<Task> _workers = new();
    private readonly CancellationTokenSource _cts = new();

    public CorrelationPipeline(
        IRuleEngine ruleEngine,
        IAlertRepository alertRepository,
        IAlertPublisher alertPublisher,
        IOptions<CorrelationPipelineOptions> options,
        ILogger<CorrelationPipeline> logger)
    {
        _ruleEngine = ruleEngine;
        _alertRepository = alertRepository;
        _alertPublisher = alertPublisher;
        _options = options.Value;
        _logger = logger;

        var capacity = Math.Max(_options.ChannelCapacity, _options.BatchSize * 2);
        _channel = Channel.CreateBounded<ConsumeResult<EventEnvelope>>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        StartWorkers();
    }

    public async Task EnqueueAsync(ConsumeResult<EventEnvelope> message, CancellationToken cancellationToken)
    {
        await _channel.Writer.WriteAsync(message, cancellationToken);
    }

    private void StartWorkers()
    {
        var workerCount = Math.Max(1, _options.MaxDegreeOfParallelism);
        _logger.LogInformation("Starting correlation pipeline with {WorkerCount} workers", workerCount);

        for (var i = 0; i < workerCount; i++)
        {
            _workers.Add(Task.Run(() => WorkerLoopAsync(_cts.Token), _cts.Token));
        }
    }

    private async Task WorkerLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new List<ConsumeResult<EventEnvelope>>(_options.BatchSize);
        var flushInterval = TimeSpan.FromMilliseconds(Math.Max(100, _options.BatchIntervalMilliseconds));
        var lastFlush = DateTime.UtcNow;

        try
        {
            while (await _channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_channel.Reader.TryRead(out var message))
                {
                    buffer.Add(message);

                    if (buffer.Count >= _options.BatchSize)
                    {
                        await ProcessBatchAsync(buffer, cancellationToken);
                        buffer.Clear();
                        lastFlush = DateTime.UtcNow;
                    }
                }

                if (buffer.Count > 0 && DateTime.UtcNow - lastFlush >= flushInterval)
                {
                    await ProcessBatchAsync(buffer, cancellationToken);
                    buffer.Clear();
                    lastFlush = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pipeline worker cancellation requested");
        }
        finally
        {
            if (buffer.Count > 0)
            {
                await ProcessBatchAsync(buffer, cancellationToken);
                buffer.Clear();
            }
        }
    }

    private async Task ProcessBatchAsync(List<ConsumeResult<EventEnvelope>> batch, CancellationToken cancellationToken)
    {
        var meaningfulEvents = batch.Where(b => b.Message?.Normalized != null).ToList();
        if (meaningfulEvents.Count == 0)
        {
            return;
        }

        foreach (var result in meaningfulEvents)
        {
            var alerts = await _ruleEngine.EvaluateEventAsync(result.Message!, cancellationToken);
            foreach (var alert in alerts)
            {
                await _alertRepository.PersistAsync(alert, cancellationToken);
                await _alertPublisher.PublishAsync(alert, cancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();

        try
        {
            await Task.WhenAll(_workers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while disposing pipeline workers");
        }
    }
}
