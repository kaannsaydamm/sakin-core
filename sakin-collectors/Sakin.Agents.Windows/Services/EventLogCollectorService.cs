using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Agents.Windows.Configuration;
using Sakin.Agents.Windows.Messaging;
using Sakin.Agents.Windows.Models;
using CollectorEventLogMode = Sakin.Agents.Windows.Configuration.EventLogMode;

namespace Sakin.Agents.Windows.Services
{
    public class EventLogCollectorService : BackgroundService
    {
        private readonly AgentOptions _agentOptions;
        private readonly EventLogCollectorOptions _collectorOptions;
        private readonly ILogger<EventLogCollectorService> _logger;
        private readonly IEventLogPublisher _publisher;
        private readonly List<EventLogWatcher> _watchers;
        private readonly Dictionary<string, long> _lastRecordIds;
        private CancellationToken _stoppingToken;

        private static readonly string[] DefaultLogNames = { "Security", "System", "Application" };
        private static readonly int[] DefaultSecurityEventIds = { 4625, 4624, 4768, 4769 };

        public EventLogCollectorService(
            IOptions<AgentOptions> agentOptions,
            IOptions<EventLogCollectorOptions> collectorOptions,
            ILogger<EventLogCollectorService> logger,
            IEventLogPublisher publisher)
        {
            _agentOptions = agentOptions.Value;
            _collectorOptions = collectorOptions.Value;
            _logger = logger;
            _publisher = publisher;
            _watchers = new List<EventLogWatcher>();
            _lastRecordIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_collectorOptions.Enabled)
            {
                _logger.LogWarning("Windows event log collection disabled via configuration");
                return;
            }

            var logConfigurations = ResolveLogConfigurations();
            if (logConfigurations.Count == 0)
            {
                _logger.LogWarning("No Windows event logs configured for collection");
                return;
            }

            _stoppingToken = stoppingToken;

            _logger.LogInformation(
                "Starting Windows event log collector. Mode={Mode}, Logs={Logs}",
                _collectorOptions.Mode,
                string.Join(", ", logConfigurations.Select(l => l.Name)));

            if (_collectorOptions.Mode == CollectorEventLogMode.RealTime)
            {
                StartWatchers(logConfigurations);

                try
                {
                    await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    // Expected on shutdown
                }
            }
            else
            {
                await RunPollingLoopAsync(logConfigurations, stoppingToken).ConfigureAwait(false);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.EventRecordWritten -= OnEventRecordWritten;
                    watcher.Enabled = false;
                    watcher.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing event log watcher");
                }
            }

            _watchers.Clear();

            await _publisher.FlushAsync(cancellationToken).ConfigureAwait(false);

            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        private IReadOnlyList<EventLogSubscriptionOptions> ResolveLogConfigurations()
        {
            var resolved = new Dictionary<string, EventLogSubscriptionOptions>(StringComparer.OrdinalIgnoreCase);

            if (_collectorOptions.Logs is { Count: > 0 })
            {
                foreach (var log in _collectorOptions.Logs)
                {
                    if (string.IsNullOrWhiteSpace(log.Name))
                    {
                        continue;
                    }

                    resolved[log.Name] = new EventLogSubscriptionOptions
                    {
                        Name = log.Name,
                        Enabled = log.Enabled,
                        EventIds = log.EventIds != null ? new List<int>(log.EventIds) : new List<int>(),
                        Query = log.Query
                    };
                }
            }

            if (_collectorOptions.LogNames is { Count: > 0 })
            {
                foreach (var name in _collectorOptions.LogNames)
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (!resolved.ContainsKey(name))
                    {
                        resolved[name] = new EventLogSubscriptionOptions
                        {
                            Name = name,
                            Enabled = true
                        };
                    }
                }
            }

            if (resolved.Count == 0)
            {
                foreach (var name in DefaultLogNames)
                {
                    resolved[name] = new EventLogSubscriptionOptions
                    {
                        Name = name,
                        Enabled = true
                    };
                }
            }

            foreach (var log in resolved.Values)
            {
                if (string.Equals(log.Name, "Security", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var eventId in DefaultSecurityEventIds)
                    {
                        if (!log.EventIds.Contains(eventId))
                        {
                            log.EventIds.Add(eventId);
                        }
                    }
                }
            }

            return resolved.Values.Where(l => l.Enabled).ToList();
        }

        private void StartWatchers(IEnumerable<EventLogSubscriptionOptions> logs)
        {
            foreach (var log in logs)
            {
                try
                {
                    var queryString = BuildQuery(log);
                    var query = new EventLogQuery(log.Name, PathType.LogName, queryString)
                    {
                        TolerateQueryErrors = true
                    };
                    var watcher = new EventLogWatcher(query);
                    watcher.EventRecordWritten += OnEventRecordWritten;
                    watcher.Enabled = true;

                    _watchers.Add(watcher);

                    _logger.LogInformation("Real-time monitoring started for log {LogName} with query {Query}", log.Name, queryString);
                }
                catch (EventLogException ex)
                {
                    _logger.LogError(ex, "Failed to initialize watcher for log {LogName}", log.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error initializing watcher for log {LogName}", log.Name);
                }
            }
        }

        private async Task RunPollingLoopAsync(IReadOnlyList<EventLogSubscriptionOptions> logs, CancellationToken stoppingToken)
        {
            var pollInterval = TimeSpan.FromMilliseconds(Math.Max(1000, _collectorOptions.PollIntervalMs));

            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var log in logs)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var entries = FetchNewEvents(log, stoppingToken);

                    foreach (var entry in entries)
                    {
                        await _publisher.PublishAsync(entry, stoppingToken).ConfigureAwait(false);
                    }
                }

                await _publisher.FlushAsync(stoppingToken).ConfigureAwait(false);

                try
                {
                    await Task.Delay(pollInterval, stoppingToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private IReadOnlyList<EventLogEntryData> FetchNewEvents(EventLogSubscriptionOptions options, CancellationToken cancellationToken)
        {
            var results = new List<EventLogEntryData>();
            var queryString = BuildQuery(options);
            var lastKnownId = _lastRecordIds.TryGetValue(options.Name, out var storedId) ? storedId : 0L;

            try
            {
                var query = new EventLogQuery(options.Name, PathType.LogName, queryString)
                {
                    ReverseDirection = true,
                    TolerateQueryErrors = true
                };

                using var reader = new EventLogReader(query);
                EventRecord? record;

                while ((record = reader.ReadEvent()) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using (record)
                    {
                        var recordId = record.RecordId ?? 0L;

                        if (lastKnownId > 0)
                        {
                            if (recordId == lastKnownId)
                            {
                                break;
                            }

                            if (recordId < lastKnownId)
                            {
                                _logger.LogInformation("Detected reset for log {LogName}. Resetting record tracker.", options.Name);
                                lastKnownId = 0;
                            }
                        }

                        var entry = ConvertRecord(record);
                        if (entry == null)
                        {
                            continue;
                        }

                        results.Add(entry);

                        if (recordId > lastKnownId)
                        {
                            lastKnownId = recordId;
                        }

                        if (results.Count >= Math.Max(1, _collectorOptions.BatchSize))
                        {
                            break;
                        }
                    }
                }
            }
            catch (EventLogException ex)
            {
                _logger.LogError(ex, "Failed to read from event log {LogName}", options.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error reading from event log {LogName}", options.Name);
            }

            if (results.Count > 0)
            {
                _lastRecordIds[options.Name] = lastKnownId;
                results.Reverse();
            }

            return results;
        }

        private void OnEventRecordWritten(object? sender, EventRecordWrittenEventArgs args)
        {
            if (_stoppingToken.IsCancellationRequested)
            {
                return;
            }

            if (args.EventException != null)
            {
                _logger.LogError(args.EventException, "Event log watcher encountered an error");
                return;
            }

            if (args.EventRecord == null)
            {
                return;
            }

            EventLogEntryData? entry = null;

            try
            {
                using (args.EventRecord)
                {
                    entry = ConvertRecord(args.EventRecord);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert real-time event log entry");
            }

            if (entry == null)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await _publisher.PublishAsync(entry, _stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // expected on shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish real-time event log entry");
                }
            }, CancellationToken.None);
        }

        private EventLogEntryData? ConvertRecord(EventRecord record)
        {
            try
            {
                var rawXml = record.ToXml();
                var timestamp = DetermineTimestamp(record.TimeCreated);
                var machineName = !string.IsNullOrWhiteSpace(record.MachineName)
                    ? record.MachineName
                    : _agentOptions.Hostname;

                return new EventLogEntryData
                {
                    LogName = record.LogName ?? string.Empty,
                    EventId = record.Id,
                    RecordId = record.RecordId,
                    ProviderName = record.ProviderName,
                    EventName = TryGetEventName(record),
                    LevelDisplayName = record.LevelDisplayName,
                    Level = record.Level,
                    Timestamp = timestamp,
                    MachineName = machineName,
                    UserName = ExtractUserName(rawXml),
                    RawXml = rawXml
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert event record {RecordId} from {LogName}", record.RecordId, record.LogName);
                return null;
            }
        }

        private static DateTimeOffset DetermineTimestamp(DateTime? timeCreated)
        {
            if (!timeCreated.HasValue)
            {
                return DateTimeOffset.UtcNow;
            }

            var timestamp = timeCreated.Value;

            if (timestamp.Kind == DateTimeKind.Unspecified)
            {
                timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Local);
            }

            return timestamp.Kind switch
            {
                DateTimeKind.Utc => new DateTimeOffset(timestamp),
                DateTimeKind.Local => new DateTimeOffset(timestamp),
                _ => new DateTimeOffset(timestamp)
            };
        }

        private static string? TryGetEventName(EventRecord record)
        {
            try
            {
                var description = record.FormatDescription();
                if (!string.IsNullOrWhiteSpace(description))
                {
                    var normalized = description.Trim();
                    var newLineIndex = normalized.IndexOfAny(new[] { '\r', '\n' });
                    return newLineIndex > 0 ? normalized[..newLineIndex] : normalized;
                }
            }
            catch (EventLogException)
            {
                // ignore
            }
            catch (Exception)
            {
                // ignore other formatting issues
            }

            return record.TaskDisplayName ?? record.OpcodeDisplayName ?? record.ProviderName;
        }

        private static string BuildQuery(EventLogSubscriptionOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.Query))
            {
                return options.Query;
            }

            if (options.EventIds is { Count: > 0 })
            {
                var filters = string.Join(" or ", options.EventIds.Select(id => $"EventID={id}"));
                return $"*[System[({filters})]]";
            }

            if (string.Equals(options.Name, "System", StringComparison.OrdinalIgnoreCase))
            {
                return "*[System[(Level=1 or Level=2 or Level=3)]]";
            }

            return "*";
        }

        private static string? ExtractUserName(string rawXml)
        {
            try
            {
                var document = XDocument.Parse(rawXml);
                var ns = (XNamespace)"http://schemas.microsoft.com/win/2004/08/events/event";

                var subjectUser = document
                    .Descendants(ns + "Data")
                    .FirstOrDefault(element => string.Equals(element.Attribute("Name")?.Value, "SubjectUserName", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(subjectUser?.Value))
                {
                    return subjectUser!.Value;
                }

                var targetUser = document
                    .Descendants(ns + "Data")
                    .FirstOrDefault(element => string.Equals(element.Attribute("Name")?.Value, "TargetUserName", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(targetUser?.Value))
                {
                    return targetUser!.Value;
                }

                var genericUser = document
                    .Descendants(ns + "Data")
                    .FirstOrDefault(element => string.Equals(element.Attribute("Name")?.Value, "User", StringComparison.OrdinalIgnoreCase));

                return genericUser?.Value;
            }
            catch
            {
                return null;
            }
        }

        public override void Dispose()
        {
            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.EventRecordWritten -= OnEventRecordWritten;
                    watcher.Dispose();
                }
                catch
                {
                    // ignore cleanup failures during dispose
                }
            }

            _watchers.Clear();

            base.Dispose();
        }
    }
}
