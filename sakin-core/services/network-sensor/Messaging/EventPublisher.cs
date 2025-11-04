using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Models;
using Sakin.Core.Sensor.Configuration;
using Sakin.Messaging.Exceptions;
using Sakin.Messaging.Producer;

namespace Sakin.Core.Sensor.Messaging;

public class EventPublisher : IEventPublisher, IDisposable
{
    private readonly IKafkaProducer _kafkaProducer;
    private readonly SensorKafkaOptions _kafkaOptions;
    private readonly ILogger<EventPublisher> _logger;
    private readonly ConcurrentQueue<PacketEventData> _queue;
    private readonly Timer? _flushTimer;
    private readonly TimeSpan _flushInterval;
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    public EventPublisher(
        IKafkaProducer kafkaProducer,
        IOptions<SensorKafkaOptions> kafkaOptions,
        ILogger<EventPublisher> logger)
    {
        _kafkaProducer = kafkaProducer;
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;
        _queue = new ConcurrentQueue<PacketEventData>();
        _semaphore = new SemaphoreSlim(1, 1);
        _flushInterval = TimeSpan.FromMilliseconds(_kafkaOptions.FlushIntervalMs);

        if (_kafkaOptions.Enabled)
        {
            _flushTimer = new Timer(async _ =>
            {
                try
                {
                    await FlushQueueAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error flushing event queue on timer");
                }
            }, null, _flushInterval, _flushInterval);

            _logger.LogInformation("EventPublisher initialized with batching enabled. BatchSize={BatchSize}, FlushInterval={FlushIntervalMs}ms",
                _kafkaOptions.BatchSize, _kafkaOptions.FlushIntervalMs);
        }
        else
        {
            _logger.LogInformation("EventPublisher initialized but Kafka is disabled");
        }
    }

    public async Task PublishPacketEventAsync(PacketEventData packetData, CancellationToken cancellationToken = default)
    {
        if (!_kafkaOptions.Enabled)
        {
            return;
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EventPublisher));
        }

        _queue.Enqueue(packetData);

        if (_queue.Count >= _kafkaOptions.BatchSize)
        {
            await FlushQueueAsync(cancellationToken);
        }
    }

    private async Task FlushQueueAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !await _semaphore.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var batch = new List<PacketEventData>();

            while (batch.Count < _kafkaOptions.BatchSize && _queue.TryDequeue(out var item))
            {
                batch.Add(item);
            }

            if (batch.Count == 0)
            {
                return;
            }

            var successCount = 0;
            var failureCount = 0;

            foreach (var packetData in batch)
            {
                try
                {
                    await PublishSingleEventAsync(packetData, cancellationToken);
                    successCount++;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex, "Failed to publish packet event after retries");
                }
            }

            _logger.LogDebug("Published batch: {SuccessCount} successful, {FailureCount} failed, Queue size: {QueueSize}",
                successCount, failureCount, _queue.Count);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task PublishSingleEventAsync(PacketEventData packetData, CancellationToken cancellationToken)
    {
        var envelope = CreateEventEnvelope(packetData);

        var maxAttempts = Math.Max(1, _kafkaOptions.RetryCount);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await _kafkaProducer.ProduceAsync(_kafkaOptions.RawEventsTopic, envelope, envelope.EventId.ToString(), cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts - 1)
            {
                var delay = TimeSpan.FromMilliseconds(Math.Max(1, _kafkaOptions.RetryBackoffMs) * (attempt + 1));
                _logger.LogWarning(ex, "Retrying Kafka publish attempt {Attempt}/{MaxAttempts} after {Delay}ms", attempt + 1, maxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish event to Kafka after {RetryCount} attempts. EventId={EventId}",
                    maxAttempts, envelope.EventId);
                _logger.LogWarning("Kafka fallback log for packet event: {PacketEvent}",
                    JsonSerializer.Serialize(packetData));
                throw;
            }
        }
    }

    private EventEnvelope CreateEventEnvelope(PacketEventData packetData)
    {
        var protocol = MapProtocol(packetData.Protocol);
        
        var metadata = packetData.Metadata != null
            ? new Dictionary<string, object>(packetData.Metadata)
            : new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(packetData.PayloadPreview))
        {
            metadata["payloadPreview"] = packetData.PayloadPreview;
        }

        if (!string.IsNullOrWhiteSpace(packetData.Sni))
        {
            metadata["sni"] = packetData.Sni;
        }

        var deviceName = metadata.TryGetValue("deviceName", out var deviceNameValue)
            ? deviceNameValue?.ToString()
            : null;

        var sensorId = metadata.TryGetValue("sensorId", out var sensorIdValue)
            ? sensorIdValue?.ToString()
            : null;

        var normalizedEvent = new NormalizedEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = packetData.Timestamp,
            EventType = EventType.NetworkTraffic,
            Severity = Severity.Info,
            SourceIp = packetData.SourceIp,
            DestinationIp = packetData.DestinationIp,
            SourcePort = packetData.SourcePort,
            DestinationPort = packetData.DestinationPort,
            Protocol = protocol,
            Payload = packetData.PayloadPreview,
            Metadata = metadata,
            DeviceName = deviceName,
            SensorId = sensorId
        };

        var rawData = new
        {
            sourceIp = packetData.SourceIp,
            destinationIp = packetData.DestinationIp,
            protocol = packetData.Protocol,
            timestamp = packetData.Timestamp,
            sourcePort = packetData.SourcePort,
            destinationPort = packetData.DestinationPort,
            rawPayload = packetData.RawPayload,
            sni = packetData.Sni,
            metadata = metadata
        };

        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "network-sensor",
            SourceType = "packet-capture",
            Raw = JsonSerializer.Serialize(rawData),
            Normalized = normalizedEvent,
            SchemaVersion = "v1.0"
        };
    }

    private static Protocol MapProtocol(string protocolName)
    {
        return protocolName.ToUpperInvariant() switch
        {
            "TCP" => Protocol.TCP,
            "UDP" => Protocol.UDP,
            "ICMP" => Protocol.ICMP,
            "HTTP" => Protocol.HTTP,
            "HTTPS" => Protocol.HTTPS,
            "DNS" => Protocol.DNS,
            "SSH" => Protocol.SSH,
            "FTP" => Protocol.FTP,
            "SMTP" => Protocol.SMTP,
            "TLS" => Protocol.TLS,
            _ => Protocol.Unknown
        };
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (!_kafkaOptions.Enabled)
        {
            return;
        }

        _logger.LogDebug("Flushing event publisher");
        
        while (!_queue.IsEmpty)
        {
            await FlushQueueAsync(cancellationToken);
        }

        await _kafkaProducer.FlushAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing EventPublisher");
        
        _disposed = true;
        _flushTimer?.Dispose();
        
        try
        {
            FlushAsync().Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing during dispose");
        }

        _semaphore?.Dispose();
    }
}
