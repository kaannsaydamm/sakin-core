using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Models;
using Sakin.Ingest.Configuration;
using Sakin.Ingest.Pipelines;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;

namespace Sakin.Ingest.Sinks
{
    public class KafkaEventSink : IEventSink, IDisposable
    {
        private readonly IKafkaProducer _producer;
        private readonly IngestOptions _ingestOptions;
        private readonly ILogger<KafkaEventSink> _logger;
        private readonly IMessageSerializer _serializer;

        public KafkaEventSink(
            IKafkaProducer producer,
            IOptions<IngestOptions> ingestOptions,
            IMessageSerializer serializer,
            ILogger<KafkaEventSink> logger)
        {
            _producer = producer;
            _ingestOptions = ingestOptions.Value;
            _serializer = serializer;
            _logger = logger;
        }

        public async Task PublishAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken = default)
        {
            try
            {
                var json = _serializer.Serialize(normalizedEvent);
                var key = normalizedEvent.SourceIp ?? normalizedEvent.Id.ToString();
                
                var result = await _producer.ProduceAsync(_ingestOptions.OutputTopic, json, key, cancellationToken);
                
                if (result.IsSuccess)
                {
                    _logger.LogDebug("Published normalized event {EventId} to topic {Topic} at offset {Offset}", 
                        normalizedEvent.Id, result.Topic, result.Offset);
                }
                else
                {
                    _logger.LogError("Failed to publish event {EventId}: {Error}", normalizedEvent.Id, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing event {EventId} to Kafka", normalizedEvent.Id);
                throw;
            }
        }

        public void Dispose()
        {
            _producer.Dispose();
        }
    }
}