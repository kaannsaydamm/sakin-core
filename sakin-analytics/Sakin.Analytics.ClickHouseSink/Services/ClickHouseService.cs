using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Sakin.Common.Configuration;
using Sakin.Common.Models;

namespace Sakin.Analytics.ClickHouseSink.Services;

public class ClickHouseService : IClickHouseService, IDisposable
{
    private readonly ILogger<ClickHouseService> _logger;
    private readonly BaselineAggregationOptions _options;
    private readonly ClickHouseConnection _connection;
    private readonly AsyncRetryPolicy _retryPolicy;

    public ClickHouseService(
        ILogger<ClickHouseService> logger,
        IOptions<BaselineAggregationOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _connection = new ClickHouseConnection(_options.ClickHouseConnectionString);
        
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, 
                        "ClickHouse operation failed. Retry {RetryCount} after {Delay}ms", 
                        retryCount, timeSpan.TotalMilliseconds);
                });
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _connection.OpenAsync(cancellationToken);
            _logger.LogInformation("ClickHouse connection established successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ClickHouse connection");
            throw;
        }
    }

    public async Task BatchInsertEventsAsync(IEnumerable<EventEnvelope> events, CancellationToken cancellationToken = default)
    {
        var eventList = events.ToList();
        if (eventList.Count == 0) return;

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                using var bulkCopy = new ClickHouseBulkCopy(_connection)
                {
                    DestinationTableName = "events",
                    BatchSize = _options.BatchSize
                };

                var rows = eventList.Select(e => new object[]
                {
                    e.EventId,
                    e.ReceivedAt.UtcDateTime,
                    e.Normalized?.Timestamp ?? DateTime.UtcNow,
                    e.Normalized?.EventType.ToString() ?? "Unknown",
                    e.Normalized?.Severity.ToString() ?? "Info",
                    e.Normalized?.SourceIp ?? string.Empty,
                    e.Normalized?.DestinationIp ?? string.Empty,
                    e.Normalized?.SourcePort ?? 0,
                    e.Normalized?.DestinationPort ?? 0,
                    e.Normalized?.Protocol.ToString() ?? "Unknown",
                    e.Normalized?.Username ?? string.Empty,
                    e.Normalized?.Hostname ?? string.Empty,
                    e.Normalized?.DeviceName ?? string.Empty,
                    e.Source,
                    e.SourceType
                });

                await bulkCopy.WriteToServerAsync(rows, cancellationToken);
            });

            _logger.LogInformation("Successfully inserted {EventCount} events into ClickHouse", eventList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to batch insert {EventCount} events into ClickHouse", eventList.Count);
            throw;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
