using System;
using Microsoft.Extensions.Logging;
using Sakin.Common.Cache;

namespace Sakin.ThreatIntelService.Services
{
    public class RedisThreatIntelRateLimiter : IThreatIntelRateLimiter
    {
        private readonly IRedisClient _redisClient;
        private readonly ILogger<RedisThreatIntelRateLimiter> _logger;

        public RedisThreatIntelRateLimiter(IRedisClient redisClient, ILogger<RedisThreatIntelRateLimiter> logger)
        {
            _redisClient = redisClient;
            _logger = logger;
        }

        public async Task<bool> TryAcquireAsync(string providerName, int dailyQuota, CancellationToken cancellationToken = default)
        {
            if (dailyQuota <= 0)
            {
                return true;
            }

            var normalizedProvider = providerName.ToLowerInvariant();
            var todayKey = DateTime.UtcNow.ToString("yyyyMMdd");
            var key = $"threatintel:ratelimit:{normalizedProvider}:{todayKey}";

            var count = await _redisClient.IncrementAsync(key);

            if (count <= 0)
            {
                _logger.LogWarning("Failed to increment rate limit counter for provider {Provider}", providerName);
                return true;
            }

            if (count == 1)
            {
                await _redisClient.KeyExpireAsync(key, TimeSpan.FromDays(1));
            }

            if (count > dailyQuota)
            {
                _logger.LogWarning("Rate limit reached for provider {Provider}: {Count}/{Limit}", providerName, count, dailyQuota);
                return false;
            }

            return true;
        }
    }
}
