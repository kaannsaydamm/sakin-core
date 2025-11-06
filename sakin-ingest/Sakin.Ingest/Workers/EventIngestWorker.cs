using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Models;
using Sakin.Ingest.Configuration;
using Sakin.Ingest.Parsers;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Producer;

namespace Sakin.Ingest.Workers;

public class EventIngestWorker(
    IKafkaConsumer consumer,
    IKafkaProducer producer,
    IOptions<IngestKafkaOptions> options,
    ParserRegistry parserRegistry,
    ILogger<EventIngestWorker> logger) : BackgroundService
{
    private readonly IKafkaConsumer _consumer = consumer;
    private readonly IKafkaProducer _producer = producer;
    private readonly IngestKafkaOptions _options = options.Value;
    private readonly ParserRegistry _parserRegistry = parserRegistry;
    private readonly ILogger<EventIngestWorker> _logger = logger;

    private string RawTopic => string.IsNullOrWhiteSpace(_options.RawEventsTopic)
        ? "raw-events"
        : _options.RawEventsTopic;

    private string NormalizedTopic => string.IsNullOrWhiteSpace(_options.NormalizedEventsTopic)
        ? "normalized-events"
        : _options.NormalizedEventsTopic;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting ingest worker with raw topic {RawTopic}, normalized topic {NormalizedTopic}, consumer group {ConsumerGroup}",
            RawTopic,
            NormalizedTopic,
            _options.ConsumerGroup);

        try
        {
            await _consumer.ConsumeAsync<EventEnvelope>(HandleMessageAsync, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Ingest worker cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in ingest worker");
            throw;
        }
    }

    private async Task HandleMessageAsync(ConsumeResult<EventEnvelope> result)
    {
        if (result.Message is null)
        {
            _logger.LogWarning("Received null message on topic {Topic}", result.Topic);
            return;
        }

        _logger.LogInformation(
            "Processing raw event {EventId} from source {Source} with sourceType {SourceType} (topic: {Topic}, partition: {Partition}, offset: {Offset})",
            result.Message.EventId,
            result.Message.Source,
            result.Message.SourceType,
            result.Topic,
            result.Partition,
            result.Offset);

        var normalizedEnvelope = await BuildNormalizedEnvelopeAsync(result.Message);
        var messageKey = normalizedEnvelope.EventId.ToString();

        await _producer.ProduceAsync(
            NormalizedTopic,
            normalizedEnvelope,
            messageKey);

        _logger.LogInformation(
            "Published normalized event {EventId} to topic {Topic}",
            normalizedEnvelope.EventId,
            NormalizedTopic);
    }

    private async Task<EventEnvelope> BuildNormalizedEnvelopeAsync(EventEnvelope envelope)
    {
        NormalizedEvent normalizedEvent;

        var parser = _parserRegistry.GetParser(envelope.SourceType);
        if (parser is not null)
        {
            try
            {
                normalizedEvent = await parser.ParseAsync(envelope);

                if (envelope.Enrichment is { Count: > 0 } enrichment)
                {
                    var mergedMetadata = new Dictionary<string, object>(enrichment);
                    foreach (var kvp in normalizedEvent.Metadata)
                    {
                        if (!mergedMetadata.ContainsKey(kvp.Key))
                        {
                            mergedMetadata[kvp.Key] = kvp.Value;
                        }
                    }
                    normalizedEvent = normalizedEvent with { Metadata = mergedMetadata };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Parser for sourceType {SourceType} failed, using passthrough normalization", envelope.SourceType);
                normalizedEvent = BuildPassthroughNormalizedEvent(envelope);
            }
        }
        else
        {
            _logger.LogWarning("No parser found for sourceType {SourceType}, using passthrough normalization", envelope.SourceType);
            normalizedEvent = BuildPassthroughNormalizedEvent(envelope);
        }

        return envelope with
        {
            Normalized = normalizedEvent
        };
    }

    private static NormalizedEvent BuildPassthroughNormalizedEvent(EventEnvelope envelope)
    {
        var normalizationMetadata = envelope.Enrichment is { Count: > 0 } enrichment
            ? new Dictionary<string, object>(enrichment)
            : new Dictionary<string, object>();

        return new NormalizedEvent
        {
            Id = envelope.EventId,
            Timestamp = envelope.ReceivedAt.UtcDateTime,
            Payload = envelope.Raw,
            Metadata = normalizationMetadata
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping ingest worker");

        try
        {
            await _producer.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while flushing producer during shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}
