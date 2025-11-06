using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Services;

namespace Sakin.Correlation.Services;

public class RedisCleanupService : BackgroundService
{
    private readonly IRedisStateManager _redisStateManager;
    private readonly IRuleLoaderServiceV2 _ruleLoader;
    private readonly ILogger<RedisCleanupService> _logger;
    private readonly AggregationOptions _options;

    public RedisCleanupService(
        IRedisStateManager redisStateManager,
        IRuleLoaderServiceV2 ruleLoader,
        ILogger<RedisCleanupService> logger,
        IOptions<AggregationOptions> options)
    {
        _redisStateManager = redisStateManager;
        _ruleLoader = ruleLoader;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Redis cleanup service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(_options.CleanupInterval), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Redis cleanup service cancellation requested.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Redis cleanup");
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task PerformCleanupAsync(CancellationToken cancellationToken)
    {
        var rules = _ruleLoader.RulesV2;
        foreach (var rule in rules)
        {
            if (rule.Condition.Aggregation != null)
            {
                try
                {
                    await _redisStateManager.CleanupExpiredWindowsAsync(
                        rule.Id, 
                        rule.Condition.Aggregation.WindowSeconds);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup expired windows for rule {RuleId}", rule.Id);
                }
            }
        }
    }
}