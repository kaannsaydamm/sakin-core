using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Agents.Windows.Configuration;
using Sakin.Agents.Windows.Models;
using Sakin.Common.Models;
using Sakin.Messaging.Producer;

namespace Sakin.Agents.Windows.Messaging
{
    public class EventLogPublisher : IEventLogPublisher
    {
        private readonly IKafkaProducer _kafkaProducer;
        private readonly EventLogKafkaOptions _kafkaOptions;
        private readonly AgentOptions _agentOptions;
        private readonly ILogger<EventLogPublisher> _logger;
        private readonly ConcurrentQueue<EventLogEntryData> _queue;
        private readonly SemaphoreSlim _semaphore;
        private readonly Timer? _flushTimer;
        private bool _disposed;

        private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(5);

        public EventLogPublisher(
            IKafkaProducer kafkaProducer,
            IOptions<EventLogKafkaOptions> kafkaOptions,
            IOptions<AgentOptions> agentOptions,
            ILogger<EventLogPublisher> logger)
        {
            _kafkaProducer = kafkaProducer;
            _kafkaOptions = kafkaOptions.Value;
            _agentOptions = agentOptions.Value;
            _logger = logger;
            _queue = new ConcurrentQueue<EventLogEntryData>();
            _semaphore = new SemaphoreSlim(1, 1);

            if (_kafkaOptions.Enabled)
            {
                var flushInterval = _kafkaOptions.FlushIntervalMs > 0
                    ? TimeSpan.FromMilliseconds(_kafkaOptions.FlushIntervalMs)
                    : DefaultFlushInterval;

                _flushTimer = new Timer(FlushTimerCallback, null, flushInterval, flushInterval);

                _logger.LogInformation(
                    "EventLogPublisher initialized. Topic={Topic}, BatchSize={BatchSize}, FlushInterval={FlushInterval}ms",
                    _kafkaOptions.Topic,
                    _kafkaOptions.BatchSize,
                    flushInterval.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("Kafka publishing disabled via configuration");
            }
        }

        public Task PublishAsync(EventLogEntryData entry, CancellationToken cancellationToken = default)
        {
            if (!_kafkaOptions.Enabled)
            {
                return Task.CompletedTask;
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(EventLogPublisher));
            }

            _queue.Enqueue(entry);

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
                _logger.LogError(ex, "Error flushing event log queue on timer");
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
                var batch = new List<EventLogEntryData>(batchSize);

                while (batch.Count < batchSize && _queue.TryDequeue(out var entry))
                {
                    batch.Add(entry);
                }

                if (batch.Count == 0)
                {
                    return;
                }

                var successCount = 0;
                var failureCount = 0;

                foreach (var entry in batch)
                {
                    try
                    {
                        await PublishSingleEventAsync(entry, cancellationToken).ConfigureAwait(false);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex, "Failed to publish event log entry after retries. Log={LogName}, EventId={EventId}", entry.LogName, entry.EventId);
                    }
                }

                _logger.LogDebug(
                    "Published windows event log batch. Success={Success}, Failed={Failed}, RemainingQueue={QueueCount}",
                    successCount,
                    failureCount,
                    _queue.Count);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task PublishSingleEventAsync(EventLogEntryData entry, CancellationToken cancellationToken)
        {
            var envelope = CreateEventEnvelope(entry);
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
                        "Retrying publish for event {EventId} (attempt {Attempt}/{MaxAttempts}) after {Delay}ms",
                        envelope.EventId,
                        attempt + 1,
                        attempts,
                        delay.TotalMilliseconds);

                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    var fallbackPayload = JsonSerializer.Serialize(entry);
                    _logger.LogWarning("Fallback logging event log entry: {Entry}", fallbackPayload);
                    throw;
                }
            }
        }

        private EventEnvelope CreateEventEnvelope(EventLogEntryData entry)
        {
            var source = string.IsNullOrWhiteSpace(_agentOptions.Hostname)
                ? entry.MachineName
                : _agentOptions.Hostname;

            var eventType = MapEventType(entry);
            var severity = MapSeverity(entry.Level);

            var metadata = new Dictionary<string, object>
            {
                ["log_name"] = entry.LogName,
                ["event_code"] = entry.EventId,
                ["event_name"] = entry.EventName ?? string.Empty,
                ["computer"] = entry.MachineName,
                ["username"] = entry.UserName ?? string.Empty,
                ["provider"] = entry.ProviderName ?? string.Empty,
                ["level"] = entry.LevelDisplayName ?? (entry.Level?.ToString() ?? string.Empty)
            };

            if (entry.RecordId.HasValue)
            {
                metadata["record_id"] = entry.RecordId.Value;
            }

            metadata["timestamp"] = entry.Timestamp.UtcDateTime;

            var normalized = new NormalizedEvent
            {
                Timestamp = entry.Timestamp.UtcDateTime,
                EventType = eventType,
                Severity = severity,
                Payload = entry.EventName,
                Metadata = metadata,
                DeviceName = entry.MachineName,
                SensorId = _agentOptions.Name
            };

            var enrichment = new Dictionary<string, object>
            {
                ["logName"] = entry.LogName,
                ["eventId"] = entry.EventId,
                ["level"] = entry.LevelDisplayName ?? (entry.Level?.ToString() ?? "Unknown")
            };

            if (!string.IsNullOrWhiteSpace(entry.UserName))
            {
                enrichment["username"] = entry.UserName;
            }

            return new EventEnvelope
            {
                Source = source,
                SourceType = "windows-eventlog",
                Raw = entry.RawXml,
                Normalized = normalized,
                Enrichment = enrichment
            };
        }

        private static EventType MapEventType(EventLogEntryData entry)
        {
            if (string.Equals(entry.LogName, "Security", StringComparison.OrdinalIgnoreCase))
            {
                return entry.EventId is 4624 or 4625 or 4768 or 4769
                    ? EventType.AuthenticationAttempt
                    : EventType.SecurityAlert;
            }

            if (string.Equals(entry.LogName, "System", StringComparison.OrdinalIgnoreCase))
            {
                return EventType.SystemLog;
            }

            return EventType.Unknown;
        }

        private static Severity MapSeverity(int? level)
        {
            return level switch
            {
                1 => Severity.Critical,
                2 => Severity.High,
                3 => Severity.Medium,
                4 => Severity.Info,
                5 => Severity.Info,
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
