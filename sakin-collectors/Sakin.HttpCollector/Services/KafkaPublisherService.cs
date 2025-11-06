using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Sakin.Common.Models;
using Sakin.HttpCollector.Configuration;
using Sakin.HttpCollector.Models;
using Sakin.Messaging.Producer;

namespace Sakin.HttpCollector.Services;

public class KafkaPublisherService : BackgroundService
{
    private readonly ChannelReader<RawLogEntry> _channelReader;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly KafkaPublisherOptions _kafkaOptions;
    private readonly ILogger<KafkaPublisherService> _logger;
    private readonly IMetricsService _metrics;

    public KafkaPublisherService(
        ChannelReader<RawLogEntry> channelReader,
        IKafkaProducer kafkaProducer,
        IOptions<KafkaPublisherOptions> kafkaOptions,
        ILogger<KafkaPublisherService> logger,
        IMetricsService metrics)
    {
        _channelReader = channelReader;
        _kafkaProducer = kafkaProducer;
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Kafka Publisher Service started");

        await foreach (var logEntry in _channelReader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var sourceType = DetectSourceType(logEntry);
                var envelope = CreateEventEnvelope(logEntry, sourceType);

                var result = await _kafkaProducer.ProduceAsync(
                    _kafkaOptions.Topic,
                    envelope,
                    key: envelope.EventId.ToString(),
                    cancellationToken: stoppingToken);

                if (result.IsSuccess)
                {
                    _metrics.IncrementKafkaMessagesPublished(_kafkaOptions.Topic);
                    _logger.LogDebug("Published event {EventId} to Kafka topic {Topic}", 
                        envelope.EventId, _kafkaOptions.Topic);
                }
                else
                {
                    _logger.LogError("Failed to publish event to Kafka: {Error}", result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing log entry from channel");
            }
        }

        _logger.LogInformation("Kafka Publisher Service stopped");
    }

    private string DetectSourceType(RawLogEntry logEntry)
    {
        var contentType = logEntry.ContentType.ToLowerInvariant();

        if (contentType.Contains("application/json"))
        {
            return "cef_json";
        }

        if (contentType.Contains("text/plain"))
        {
            if (logEntry.RawMessage.StartsWith("CEF:0", StringComparison.OrdinalIgnoreCase) ||
                logEntry.RawMessage.StartsWith("CEF:", StringComparison.OrdinalIgnoreCase))
            {
                return "cef_string";
            }
            return "syslog_string";
        }

        if (contentType.Contains("application/x-www-form-urlencoded"))
        {
            if (logEntry.RawMessage.Contains("CEF:", StringComparison.OrdinalIgnoreCase))
            {
                return "cef_string";
            }
            return "syslog_string";
        }

        return "unknown";
    }

    private EventEnvelope CreateEventEnvelope(RawLogEntry logEntry, string sourceType)
    {
        var source = logEntry.XSourceHeader ?? logEntry.SourceIp;

        var normalized = new NormalizedEvent
        {
            SourceIp = logEntry.SourceIp,
            Timestamp = logEntry.ReceivedAt.UtcDateTime
        };

        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = logEntry.ReceivedAt,
            Source = source,
            SourceType = sourceType,
            Raw = logEntry.RawMessage,
            Normalized = normalized,
            SchemaVersion = "v1.0"
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Kafka Publisher Service stopping, flushing remaining messages...");
        await _kafkaProducer.FlushAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
