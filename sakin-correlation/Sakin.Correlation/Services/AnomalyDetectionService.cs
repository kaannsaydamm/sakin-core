using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Cache;
using Sakin.Common.Configuration;
using Sakin.Common.Models;

namespace Sakin.Correlation.Services;

public class AnomalyDetectionService : IAnomalyDetectionService
{
    private readonly ILogger<AnomalyDetectionService> _logger;
    private readonly IRedisClient _redisClient;
    private readonly IMemoryCache _memoryCache;
    private readonly AnomalyDetectionOptions _options;

    public AnomalyDetectionService(
        ILogger<AnomalyDetectionService> logger,
        IRedisClient redisClient,
        IMemoryCache memoryCache,
        IOptions<AnomalyDetectionOptions> options)
    {
        _logger = logger;
        _redisClient = redisClient;
        _memoryCache = memoryCache;
        _options = options.Value;
    }

    public async Task<AnomalyScoreResult> CalculateAnomalyScoreAsync(
        NormalizedEvent normalizedEvent, 
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return CreateNoAnomalyResult("Anomaly detection is disabled");
        }

        if (string.IsNullOrWhiteSpace(normalizedEvent.Username))
        {
            return CreateNoAnomalyResult("No username available for anomaly detection");
        }

        var cacheKey = $"anomaly:{normalizedEvent.Username}:{normalizedEvent.Hostname}:{normalizedEvent.Timestamp:yyyyMMddHH}";
        
        if (_memoryCache.TryGetValue<AnomalyScoreResult>(cacheKey, out var cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Returning cached anomaly score for {User}", normalizedEvent.Username);
            return cachedResult;
        }

        var anomalyResult = await DetectAnomaliesAsync(normalizedEvent, cancellationToken);

        _memoryCache.Set(cacheKey, anomalyResult, TimeSpan.FromSeconds(_options.CacheDurationSeconds));

        return anomalyResult;
    }

    private async Task<AnomalyScoreResult> DetectAnomaliesAsync(
        NormalizedEvent normalizedEvent, 
        CancellationToken cancellationToken)
    {
        var anomalies = new List<AnomalyScoreResult>();

        var hourlyActivityAnomaly = await CheckHourlyActivityAnomalyAsync(normalizedEvent, cancellationToken);
        if (hourlyActivityAnomaly.IsAnomalous)
        {
            anomalies.Add(hourlyActivityAnomaly);
        }

        if (!string.IsNullOrWhiteSpace(normalizedEvent.Hostname))
        {
            var connectionAnomaly = await CheckConnectionCountAnomalyAsync(normalizedEvent, cancellationToken);
            if (connectionAnomaly.IsAnomalous)
            {
                anomalies.Add(connectionAnomaly);
            }

            var portsAnomaly = await CheckUniquePortsAnomalyAsync(normalizedEvent, cancellationToken);
            if (portsAnomaly.IsAnomalous)
            {
                anomalies.Add(portsAnomaly);
            }
        }

        if (anomalies.Count == 0)
        {
            return CreateNoAnomalyResult("No anomalies detected");
        }

        var maxAnomaly = anomalies.OrderByDescending(a => a.Score).First();
        var allReasons = string.Join("; ", anomalies.Select(a => a.Reasoning));

        return new AnomalyScoreResult
        {
            Score = maxAnomaly.Score,
            IsAnomalous = true,
            ZScore = maxAnomaly.ZScore,
            Reasoning = allReasons,
            BaselineMean = maxAnomaly.BaselineMean,
            BaselineStdDev = maxAnomaly.BaselineStdDev,
            CurrentValue = maxAnomaly.CurrentValue,
            MetricName = "multiple_metrics"
        };
    }

    private async Task<AnomalyScoreResult> CheckHourlyActivityAnomalyAsync(
        NormalizedEvent normalizedEvent, 
        CancellationToken cancellationToken)
    {
        var hour = normalizedEvent.Timestamp.Hour;
        var redisKey = $"{_options.RedisKeyPrefix}:user_hour:{normalizedEvent.Username}:{hour}";

        var baseline = await _redisClient.GetAsync<BaselineFeatureSnapshot>(redisKey, cancellationToken);
        
        if (baseline == null)
        {
            return CreateNoAnomalyResult("No baseline data available for hourly activity");
        }

        var currentValue = 1.0;
        var zScore = CalculateZScore(currentValue, baseline.Mean, baseline.StdDev);
        
        if (Math.Abs(zScore) < _options.ZScoreThreshold)
        {
            return CreateNoAnomalyResult("Activity within normal range");
        }

        var score = NormalizeZScoreToScore(zScore);
        var reasoning = $"Activity at hour {hour} is {Math.Abs(zScore):F2} standard deviations from normal (mean: {baseline.Mean:F2}, current: {currentValue})";

        return new AnomalyScoreResult
        {
            Score = score,
            IsAnomalous = true,
            ZScore = zScore,
            Reasoning = reasoning,
            BaselineMean = baseline.Mean,
            BaselineStdDev = baseline.StdDev,
            CurrentValue = currentValue,
            MetricName = "hourly_activity"
        };
    }

    private async Task<AnomalyScoreResult> CheckConnectionCountAnomalyAsync(
        NormalizedEvent normalizedEvent, 
        CancellationToken cancellationToken)
    {
        var redisKey = $"{_options.RedisKeyPrefix}:user_host_conn:{normalizedEvent.Username}:{normalizedEvent.Hostname}";

        var baseline = await _redisClient.GetAsync<BaselineFeatureSnapshot>(redisKey, cancellationToken);
        
        if (baseline == null)
        {
            return CreateNoAnomalyResult("No baseline data available for connection count");
        }

        var currentValue = 1.0;
        var zScore = CalculateZScore(currentValue, baseline.Mean, baseline.StdDev);
        
        if (Math.Abs(zScore) < _options.ZScoreThreshold)
        {
            return CreateNoAnomalyResult("Connection count within normal range");
        }

        var score = NormalizeZScoreToScore(zScore);
        var reasoning = $"Connection count from {normalizedEvent.Username}@{normalizedEvent.Hostname} is {Math.Abs(zScore):F2} standard deviations from normal (mean: {baseline.Mean:F2})";

        return new AnomalyScoreResult
        {
            Score = score,
            IsAnomalous = true,
            ZScore = zScore,
            Reasoning = reasoning,
            BaselineMean = baseline.Mean,
            BaselineStdDev = baseline.StdDev,
            CurrentValue = currentValue,
            MetricName = "connection_count"
        };
    }

    private async Task<AnomalyScoreResult> CheckUniquePortsAnomalyAsync(
        NormalizedEvent normalizedEvent, 
        CancellationToken cancellationToken)
    {
        if (!normalizedEvent.DestinationPort.HasValue || normalizedEvent.DestinationPort.Value == 0)
        {
            return CreateNoAnomalyResult("No destination port available");
        }

        var redisKey = $"{_options.RedisKeyPrefix}:user_host_ports:{normalizedEvent.Username}:{normalizedEvent.Hostname}";

        var baseline = await _redisClient.GetAsync<BaselineFeatureSnapshot>(redisKey, cancellationToken);
        
        if (baseline == null)
        {
            return CreateNoAnomalyResult("No baseline data available for unique ports");
        }

        var currentValue = 1.0;
        var zScore = CalculateZScore(currentValue, baseline.Mean, baseline.StdDev);
        
        if (Math.Abs(zScore) < _options.ZScoreThreshold)
        {
            return CreateNoAnomalyResult("Unique ports within normal range");
        }

        var score = NormalizeZScoreToScore(zScore);
        var reasoning = $"Unique destination ports for {normalizedEvent.Username}@{normalizedEvent.Hostname} is {Math.Abs(zScore):F2} standard deviations from normal (mean: {baseline.Mean:F2})";

        return new AnomalyScoreResult
        {
            Score = score,
            IsAnomalous = true,
            ZScore = zScore,
            Reasoning = reasoning,
            BaselineMean = baseline.Mean,
            BaselineStdDev = baseline.StdDev,
            CurrentValue = currentValue,
            MetricName = "unique_ports"
        };
    }

    private static double CalculateZScore(double value, double mean, double stdDev)
    {
        if (stdDev == 0)
        {
            return 0;
        }
        return (value - mean) / stdDev;
    }

    private static double NormalizeZScoreToScore(double zScore)
    {
        var absZScore = Math.Abs(zScore);
        
        if (absZScore < 2.5)
            return 0;
        if (absZScore >= 5.0)
            return 100;
        
        return ((absZScore - 2.5) / 2.5) * 100.0;
    }

    private static AnomalyScoreResult CreateNoAnomalyResult(string reasoning)
    {
        return new AnomalyScoreResult
        {
            Score = 0,
            IsAnomalous = false,
            ZScore = 0,
            Reasoning = reasoning,
            MetricName = "none"
        };
    }
}
