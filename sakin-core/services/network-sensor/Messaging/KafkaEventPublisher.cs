using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Models;
using Sakin.Core.Sensor.Configuration;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Exceptions;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;

namespace Sakin.Core.Sensor.Messaging
{
    public class KafkaEventPublisher : BackgroundService, IEventPublisher
    {
        private readonly IKafkaProducer _producer;
        private readonly IMessageSerializer _serializer;
        private readonly ILogger<KafkaEventPublisher> _logger;
        private readonly ProducerOptions _producerOptions;
        private readonly SensorOptions _sensorOptions;
        private readonly Channel<NetworkEvent> _channel;
        private readonly TimeSpan _linger;
        public PublishMetrics Metrics { get; } = new();
        public string TopicName { get; }

        public KafkaEventPublisher(
            IKafkaProducer producer,
            IOptions<ProducerOptions> producerOptions,
            IOptions<SensorOptions> sensorOptions,
            IMessageSerializer serializer,
            ILogger<KafkaEventPublisher> logger)
        {
            _producer = producer;
            _serializer = serializer;
            _logger = logger;
            _producerOptions = producerOptions.Value;
            _sensorOptions = sensorOptions.Value;
            TopicName = _producerOptions.DefaultTopic;
            _linger = TimeSpan.FromMilliseconds(Math.Max(1, _producerOptions.LingerMs));

            var capacity = Math.Max(1000, _producerOptions.BatchSize * 10);
            _channel = Channel.CreateBounded<NetworkEvent>(new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropWrite
            });
        }

        public bool Enqueue(NetworkEvent networkEvent)
        {
            // Non-blocking write; if drop occurs, count as failed and persist to fallback
            if (!_channel.Writer.TryWrite(networkEvent))
            {
                Metrics.IncrementAttempted();
                Metrics.IncrementFailed();
                PersistFallback(networkEvent, new InvalidOperationException("Publisher queue full"));
                return false;
            }
            return true;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("KafkaEventPublisher started. Topic={Topic}, BatchSize={BatchSize}, LingerMs={LingerMs}",
                TopicName, _producerOptions.BatchSize, _producerOptions.LingerMs);

            var metricsInterval = TimeSpan.FromSeconds(Math.Max(5, _sensorOptions.MetricsLogIntervalSeconds));
            var nextMetricsAt = DateTime.UtcNow + metricsInterval;

            try
            {
                var reader = _channel.Reader;
                while (!stoppingToken.IsCancellationRequested)
                {
                    List<NetworkEvent> batch = new();

                    // Block until at least one item is available or cancellation requested
                    NetworkEvent first;
                    try
                    {
                        first = await reader.ReadAsync(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    batch.Add(first);
                    var started = DateTime.UtcNow;

                    // Drain quickly up to batch size or linger timeout
                    while (batch.Count < _producerOptions.BatchSize && (DateTime.UtcNow - started) < _linger && reader.TryRead(out var next))
                    {
                        batch.Add(next);
                    }

                    // Publish batch
                    await PublishBatchAsync(batch, stoppingToken);

                    if (DateTime.UtcNow >= nextMetricsAt)
                    {
                        LogMetrics();
                        nextMetricsAt = DateTime.UtcNow + metricsInterval;
                    }
                }

                // Flush remaining items on shutdown
                var remaining = new List<NetworkEvent>();
                while (_channel.Reader.TryRead(out var item))
                {
                    remaining.Add(item);
                    if (remaining.Count >= _producerOptions.BatchSize)
                    {
                        await PublishBatchAsync(remaining, stoppingToken);
                        remaining.Clear();
                    }
                }
                if (remaining.Count > 0)
                {
                    await PublishBatchAsync(remaining, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in KafkaEventPublisher background loop");
            }
        }

        private async Task PublishBatchAsync(List<NetworkEvent> batch, CancellationToken cancellationToken)
        {
            if (batch.Count == 0) return;

            Metrics.IncrementAttempted(batch.Count);

            foreach (var ev in batch)
            {
                try
                {
                    var key = ev.SourceIp + ":" + ev.DestinationIp + ":" + ev.Protocol.ToString();
                    await _producer.ProduceAsync(TopicName, ev, key, cancellationToken);
                    Metrics.IncrementDelivered();
                }
                catch (KafkaProducerException kpe)
                {
                    _logger.LogWarning(kpe, "Kafka producer error; persisting event to fallback");
                    Metrics.IncrementFailed();
                    PersistFallback(ev, kpe);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while producing event; persisting to fallback");
                    Metrics.IncrementFailed();
                    PersistFallback(ev, ex);
                }
            }
        }

        private void PersistFallback(NetworkEvent ev, Exception? reason)
        {
            try
            {
                var path = _sensorOptions.Fallback.Path;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Include minimal error metadata
                var data = new Dictionary<string, object?>
                {
                    ["event"] = ev,
                    ["error"] = reason?.Message,
                    ["ts"] = DateTime.UtcNow
                };
                var bytes = _serializer.Serialize(data);
                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream);
                writer.WriteLine(System.Text.Encoding.UTF8.GetString(bytes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist event to fallback store");
            }
        }

        private void LogMetrics()
        {
            var attempted = Metrics.Attempted;
            var delivered = Metrics.Delivered;
            var failed = Metrics.Failed;
            var rate = Metrics.SuccessRate;
            _logger.LogInformation("Kafka publish metrics: attempted={Attempted}, delivered={Delivered}, failed={Failed}, successRate={Rate,0:F2}%",
                attempted, delivered, failed, rate);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _channel.Writer.Complete();
            await base.StopAsync(cancellationToken);
            try
            {
                await _producer.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during producer flush on shutdown");
            }
        }
    }
}
