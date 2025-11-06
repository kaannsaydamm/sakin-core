using ClickHouse.Client.ADO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Sakin.Common.Cache;
using Sakin.Common.Configuration;
using Sakin.Common.Models;

namespace Sakin.Analytics.BaselineWorker.Services;

public class BaselineCalculatorService : IBaselineCalculatorService
{
    private readonly ILogger<BaselineCalculatorService> _logger;
    private readonly IRedisClient _redisClient;
    private readonly BaselineAggregationOptions _options;
    private readonly ClickHouseConnection _connection;
    private readonly AsyncRetryPolicy _retryPolicy;

    public BaselineCalculatorService(
        ILogger<BaselineCalculatorService> logger,
        IRedisClient redisClient,
        IOptions<BaselineAggregationOptions> options)
    {
        _logger = logger;
        _redisClient = redisClient;
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
                        "ClickHouse query failed. Retry {RetryCount} after {Delay}ms", 
                        retryCount, timeSpan.TotalMilliseconds);
                });
    }

    public async Task CalculateAndStoreBaselinesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting baseline calculation...");

        try
        {
            await _connection.OpenAsync(cancellationToken);

            await CalculateUserHourlyActivityBaselineAsync(cancellationToken);
            await CalculateConnectionCountBaselineAsync(cancellationToken);
            await CalculateUniquePortsBaselineAsync(cancellationToken);

            _logger.LogInformation("Baseline calculation completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate baselines");
            throw;
        }
        finally
        {
            await _connection.CloseAsync();
        }
    }

    private async Task CalculateUserHourlyActivityBaselineAsync(CancellationToken cancellationToken)
    {
        var query = $@"
            SELECT 
                username,
                toHour(event_timestamp) as hour_of_day,
                count() as event_count,
                avg(event_count) as mean,
                stddevPop(event_count) as stddev,
                min(event_count) as min_val,
                max(event_count) as max_val
            FROM (
                SELECT 
                    username,
                    event_timestamp,
                    count() as event_count
                FROM events
                WHERE event_timestamp >= now() - INTERVAL {_options.AnalysisWindowDays} DAY
                  AND username != ''
                GROUP BY username, toStartOfHour(event_timestamp) as event_timestamp
            )
            GROUP BY username, hour_of_day
            HAVING count() >= 3";

        await ExecuteBaselineQueryAsync(query, "user_hour", cancellationToken);
    }

    private async Task CalculateConnectionCountBaselineAsync(CancellationToken cancellationToken)
    {
        var query = $@"
            SELECT 
                username,
                hostname,
                count() as conn_count,
                avg(conn_count) as mean,
                stddevPop(conn_count) as stddev,
                min(conn_count) as min_val,
                max(conn_count) as max_val
            FROM (
                SELECT 
                    username,
                    hostname,
                    count() as conn_count
                FROM events
                WHERE event_timestamp >= now() - INTERVAL {_options.AnalysisWindowDays} DAY
                  AND username != ''
                  AND hostname != ''
                GROUP BY username, hostname, toStartOfHour(event_timestamp)
            )
            GROUP BY username, hostname
            HAVING count() >= 3";

        await ExecuteBaselineQueryAsync(query, "user_host_conn", cancellationToken);
    }

    private async Task CalculateUniquePortsBaselineAsync(CancellationToken cancellationToken)
    {
        var query = $@"
            SELECT 
                username,
                hostname,
                uniq(destination_port) as unique_ports,
                avg(unique_ports) as mean,
                stddevPop(unique_ports) as stddev,
                min(unique_ports) as min_val,
                max(unique_ports) as max_val
            FROM (
                SELECT 
                    username,
                    hostname,
                    uniq(destination_port) as unique_ports
                FROM events
                WHERE event_timestamp >= now() - INTERVAL {_options.AnalysisWindowDays} DAY
                  AND username != ''
                  AND hostname != ''
                  AND destination_port > 0
                GROUP BY username, hostname, toStartOfHour(event_timestamp)
            )
            GROUP BY username, hostname
            HAVING count() >= 3";

        await ExecuteBaselineQueryAsync(query, "user_host_ports", cancellationToken);
    }

    private async Task ExecuteBaselineQueryAsync(string query, string metricPrefix, CancellationToken cancellationToken)
    {
        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                using var command = _connection.CreateCommand();
                command.CommandText = query;

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var count = 0;

                while (await reader.ReadAsync(cancellationToken))
                {
                    var baseline = new BaselineFeatureSnapshot
                    {
                        Mean = reader.IsDBNull(reader.GetOrdinal("mean")) ? 0 : reader.GetDouble(reader.GetOrdinal("mean")),
                        StdDev = reader.IsDBNull(reader.GetOrdinal("stddev")) ? 0 : reader.GetDouble(reader.GetOrdinal("stddev")),
                        Count = reader.IsDBNull(reader.GetOrdinal("mean")) ? 0 : reader.GetInt64(0),
                        Min = reader.IsDBNull(reader.GetOrdinal("min_val")) ? 0 : reader.GetDouble(reader.GetOrdinal("min_val")),
                        Max = reader.IsDBNull(reader.GetOrdinal("max_val")) ? 0 : reader.GetDouble(reader.GetOrdinal("max_val")),
                        CalculatedAt = DateTime.UtcNow
                    };

                    var keyParts = new List<string> { "sakin:baseline", metricPrefix };
                    
                    for (int i = 0; i < reader.FieldCount - 5; i++)
                    {
                        var value = reader.GetValue(i);
                        keyParts.Add(value?.ToString() ?? "unknown");
                    }

                    var redisKey = string.Join(":", keyParts);
                    
                    await _redisClient.SetAsync(
                        redisKey, 
                        baseline, 
                        TimeSpan.FromHours(_options.BaselineTtlHours),
                        cancellationToken);
                    
                    count++;
                }

                _logger.LogInformation("Stored {Count} baseline snapshots for {Metric}", count, metricPrefix);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate baseline for {Metric}", metricPrefix);
        }
    }
}
