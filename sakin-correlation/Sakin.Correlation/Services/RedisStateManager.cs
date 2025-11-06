using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Cache;
using Sakin.Correlation.Configuration;

namespace Sakin.Correlation.Services;

public interface IRedisStateManager
{
    Task<long> IncrementCounterAsync(string ruleId, string groupValue, long windowId);
    Task<long> GetCountAsync(string ruleId, string groupValue, long windowId);
    Task CleanupExpiredWindowsAsync(string ruleId, int windowSeconds);
}

public class RedisStateManager : IRedisStateManager
{
    private readonly IRedisClient _redisClient;
    private readonly ILogger<RedisStateManager> _logger;
    private readonly RedisOptions _options;

    public RedisStateManager(
        IRedisClient redisClient,
        ILogger<RedisStateManager> logger,
        IOptions<RedisOptions> options)
    {
        _redisClient = redisClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<long> IncrementCounterAsync(string ruleId, string groupValue, long windowId)
    {
        var key = BuildKey(ruleId, groupValue, windowId);
        
        try
        {
            var count = await _redisClient.IncrementAsync(key);
            
            // Set TTL on first increment to ensure cleanup
            if (count == 1)
            {
                await _redisClient.StringSetAsync(key, count.ToString(), TimeSpan.FromSeconds(_options.DefaultTTL));
            }
            
            _logger.LogDebug("Incremented counter for key {Key} to {Count}", key, count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment counter for key {Key}", key);
            throw;
        }
    }

    public async Task<long> GetCountAsync(string ruleId, string groupValue, long windowId)
    {
        var key = BuildKey(ruleId, groupValue, windowId);
        
        try
        {
            var countStr = await _redisClient.StringGetAsync(key);
            var count = long.TryParse(countStr, out var parsed) ? parsed : 0;
            
            _logger.LogDebug("Retrieved count for key {Key}: {Count}", key, count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get count for key {Key}", key);
            throw;
        }
    }

    public async Task CleanupExpiredWindowsAsync(string ruleId, int windowSeconds)
    {
        // This is a simplified cleanup - in production you might want to use Redis SCAN
        // For now, we rely on TTL to automatically clean up expired keys
        _logger.LogDebug("Cleanup called for rule {RuleId} with window size {WindowSeconds} seconds", 
            ruleId, windowSeconds);
        
        await Task.CompletedTask;
    }

    private string BuildKey(string ruleId, string groupValue, long windowId)
    {
        return $"{_options.KeyPrefix}rule:{ruleId}:group:{groupValue}:window:{windowId}";
    }
}