using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Ingest.Configuration;
using Sakin.Ingest.Pipelines;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using System.Text.Json;

namespace Sakin.Ingest.Sources
{
    public class KafkaEventSource : IEventSource, IDisposable
    {
        private readonly IKafkaConsumer _consumer;
        private readonly IngestOptions _ingestOptions;
        private readonly ILogger<KafkaEventSource> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private Task? _consumerTask;

        public event EventHandler<RawEvent>? OnRawEventReceived;

        public KafkaEventSource(
            IKafkaConsumer consumer,
            IOptions<IngestOptions> ingestOptions,
            ILogger<KafkaEventSource> logger)
        {
            _consumer = consumer;
            _ingestOptions = ingestOptions.Value;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting Kafka event source for topic {Topic}", _ingestOptions.InputTopic);

            try
            {
                _consumer.Subscribe(_ingestOptions.InputTopic);
                
                _consumerTask = Task.Run(async () =>
                {
                    await _consumer.ConsumeAsync<string>(async (message) =>
                    {
                        try
                        {
                            var rawEvent = new RawEvent
                            {
                                Id = Guid.NewGuid().ToString(),
                                Timestamp = DateTime.UtcNow,
                                Source = "kafka",
                                Format = "JSON",
                                Data = message.Message ?? string.Empty,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["topic"] = message.Topic,
                                    ["partition"] = message.Partition,
                                    ["offset"] = message.Offset,
                                    ["key"] = message.Key ?? string.Empty
                                }
                            };

                            OnRawEventReceived?.Invoke(this, rawEvent);
                            _logger.LogDebug("Processed raw event from Kafka: {EventId}", rawEvent.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing Kafka message at offset {Offset}", message.Offset);
                        }
                    }, _cancellationTokenSource.Token);
                }, cancellationToken);

                _logger.LogInformation("Kafka event source started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Kafka event source");
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Stopping Kafka event source");

            try
            {
                _cancellationTokenSource.Cancel();
                
                if (_consumerTask != null)
                {
                    await _consumerTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                }

                _consumer.Unsubscribe();
                _logger.LogInformation("Kafka event source stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Kafka event source");
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _consumer.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }
}