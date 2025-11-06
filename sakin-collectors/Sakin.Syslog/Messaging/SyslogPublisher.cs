using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Models;
using Sakin.Messaging.Producer;
using Sakin.Syslog.Configuration;
using Sakin.Syslog.Models;

namespace Sakin.Syslog.Messaging
{
    public interface ISyslogPublisher
    {
        Task PublishAsync(SyslogMessage message, CancellationToken cancellationToken = default);
        Task FlushAsync(CancellationToken cancellationToken = default);
    }
    
    public class SyslogPublisher : ISyslogPublisher, IDisposable
    {
        private readonly IKafkaProducer _kafkaProducer;
        private readonly SyslogKafkaOptions _kafkaOptions;
        private readonly AgentOptions _agentOptions;
        private readonly ILogger<SyslogPublisher> _logger;
        private readonly ConcurrentQueue<SyslogMessage> _queue;
        private readonly SemaphoreSlim _semaphore;
        private readonly Timer? _flushTimer;
        private bool _disposed;
        
        private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(5);
        
        public SyslogPublisher(
            IKafkaProducer kafkaProducer,
            IOptions<SyslogKafkaOptions> kafkaOptions,
            IOptions<AgentOptions> agentOptions,
            ILogger<SyslogPublisher> logger)
        {
            _kafkaProducer = kafkaProducer;
            _kafkaOptions = kafkaOptions.Value;
            _agentOptions = agentOptions.Value;
            _logger = logger;
            _queue = new ConcurrentQueue<SyslogMessage>();
            _semaphore = new SemaphoreSlim(1, 1);
            
            if (_kafkaOptions.Enabled)
            {
                var flushInterval = _kafkaOptions.FlushIntervalMs > 0
                    ? TimeSpan.FromMilliseconds(_kafkaOptions.FlushIntervalMs)
                    : DefaultFlushInterval;

                _flushTimer = new Timer(FlushTimerCallback, null, flushInterval, flushInterval);
                
                _logger.LogInformation(
                    "SyslogPublisher initialized. Topic={Topic}, BatchSize={BatchSize}, FlushInterval={FlushInterval}ms",
                    _kafkaOptions.Topic,
                    _kafkaOptions.BatchSize,
                    flushInterval.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("Kafka publishing disabled via configuration");
            }
        }
        
        public Task PublishAsync(SyslogMessage message, CancellationToken cancellationToken = default)
        {
            if (!_kafkaOptions.Enabled)
            {
                return Task.CompletedTask;
            }
            
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SyslogPublisher));
            }
            
            _queue.Enqueue(message);
            
            // Simple batch size threshold
            if (_queue.Count >= Math.Max(1, _kafkaOptions.BatchSize))
            {
                return FlushQueueAsync(cancellationToken);
            }
            
            return Task.CompletedTask;
        }
        
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            if (!_kafkaOptions.Enabled)
            {
                return;
            }
            
            await FlushQueueAsync(cancellationToken).ConfigureAwait(false);
            await _kafkaProducer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        
        private async void FlushTimerCallback(object? state)
        {
            if (_disposed)
            {
                return;
            }
            
            try
            {
                await FlushQueueAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing syslog queue on timer");
            }
        }
        
        private async Task FlushQueueAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed || _queue.IsEmpty)
            {
                return;
            }
            
            if (!await _semaphore.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
            
            try
            {
                var batchSize = Math.Max(1, _kafkaOptions.BatchSize);
                var batch = new List<SyslogMessage>(batchSize);
                
                while (batch.Count < batchSize && _queue.TryDequeue(out var message))
                {
                    batch.Add(message);
                }
                
                if (batch.Count == 0)
                {
                    return;
                }
                
                var successCount = 0;
                var failureCount = 0;
                
                foreach (var message in batch)
                {
                    try
                    {
                        await PublishSingleEventAsync(message, cancellationToken).ConfigureAwait(false);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex, "Failed to publish syslog message from {Endpoint}", message.RemoteEndpoint);
                    }
                }
                
                _logger.LogDebug(
                    "Published syslog batch. Success={Success}, Failed={Failed}, RemainingQueue={QueueCount}",
                    successCount,
                    failureCount,
                    _queue.Count);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        private async Task PublishSingleEventAsync(SyslogMessage message, CancellationToken cancellationToken)
        {
            var envelope = CreateEventEnvelope(message);
            
            // Simple retry logic
            var attempts = Math.Max(1, _kafkaOptions.RetryCount);
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    await _kafkaProducer
                        .ProduceAsync(_kafkaOptions.Topic, envelope, envelope.EventId.ToString(), cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (attempt < attempts - 1)
                {
                    var delay = TimeSpan.FromMilliseconds(Math.Max(1, _kafkaOptions.RetryBackoffMs) * (attempt + 1));
                    _logger.LogWarning(ex,
                        "Retrying publish for syslog message from {Endpoint} (attempt {Attempt}/{MaxAttempts}) after {Delay}ms",
                        message.RemoteEndpoint,
                        attempt + 1,
                        attempts,
                        delay.TotalMilliseconds);
                    
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    var fallbackPayload = JsonSerializer.Serialize(message);
                    _logger.LogWarning("Fallback logging syslog message: {Message}", fallbackPayload);
                    throw;
                }
            }
        }
        
        private EventEnvelope CreateEventEnvelope(SyslogMessage message)
        {
            var source = string.IsNullOrWhiteSpace(_agentOptions.Hostname)
                ? message.Hostname
                : _agentOptions.Hostname;
            
            var eventType = MapEventType(message);
            var severity = MapSeverity(message.Severity);
            
            var metadata = new Dictionary<string, object>
            {
                ["hostname"] = message.Hostname,
                ["tag"] = message.Tag,
                ["priority"] = message.Priority,
                ["facility"] = message.Facility,
                ["severity"] = message.Severity,
                ["remote_endpoint"] = message.RemoteEndpoint,
                ["timestamp"] = message.Timestamp.UtcDateTime
            };
            
            var normalized = new NormalizedEvent
            {
                Timestamp = message.Timestamp.UtcDateTime,
                EventType = eventType,
                Severity = severity,
                Payload = message.Message,
                Metadata = metadata,
                DeviceName = message.Hostname,
                SensorId = _agentOptions.Name
            };
            
            var enrichment = new Dictionary<string, object>
            {
                ["facility"] = message.Facility,
                ["severity"] = message.Severity,
                ["priority"] = message.Priority,
                ["tag"] = message.Tag
            };
            
            if (!string.IsNullOrWhiteSpace(message.RemoteEndpoint))
            {
                enrichment["remoteEndpoint"] = message.RemoteEndpoint;
            }
            
            return new EventEnvelope
            {
                Source = source,
                SourceType = "syslog",
                Raw = message.Raw,
                Normalized = normalized,
                Enrichment = enrichment
            };
        }
        
        private static EventType MapEventType(SyslogMessage message)
        {
            return message.Severity switch
            {
                0 or 1 or 2 => EventType.SecurityAlert, // Emergency, Alert, Critical
                3 => EventType.SecurityAlert, // Error
                4 => EventType.SystemLog, // Warning
                5 => EventType.SystemLog, // Notice
                6 => EventType.SystemLog, // Informational
                7 => EventType.SystemLog, // Debug
                _ => EventType.Unknown
            };
        }
        
        private static Severity MapSeverity(int severity)
        {
            return severity switch
            {
                0 or 1 or 2 => Severity.Critical, // Emergency, Alert, Critical
                3 => Severity.High, // Error
                4 => Severity.Medium, // Warning
                5 or 6 => Severity.Info, // Notice, Informational
                7 => Severity.Info, // Debug
                _ => Severity.Unknown
            };
        }
        
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            
            _disposed = true;
            
            _flushTimer?.Dispose();
            _semaphore.Dispose();
        }
    }
}