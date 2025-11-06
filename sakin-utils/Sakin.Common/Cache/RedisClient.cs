using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Configuration;
using StackExchange.Redis;

namespace Sakin.Common.Cache
{
    public class RedisClient : IRedisClient, IDisposable
    {
        private readonly RedisOptions _options;
        private readonly ILogger<RedisClient>? _logger;
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _database;

        public RedisClient(IOptions<RedisOptions> options, ILogger<RedisClient>? logger = null)
        {
            _options = options.Value;
            _logger = logger;

            try
            {
                _redis = ConnectionMultiplexer.Connect(_options.ConnectionString);
                _database = _redis.GetDatabase();
                _logger?.LogInformation("Redis connection established successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while connecting to Redis: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null)
        {
            try
            {
                return await _database.StringSetAsync(key, value, expiry);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error setting Redis key {Key}: {Message}", key, ex.Message);
                return false;
            }
        }

        public async Task<string?> StringGetAsync(string key)
        {
            try
            {
                var value = await _database.StringGetAsync(key);
                return value.HasValue ? value.ToString() : null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting Redis key {Key}: {Message}", key, ex.Message);
                return null;
            }
        }

        public async Task<bool> KeyDeleteAsync(string key)
        {
            try
            {
                return await _database.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting Redis key {Key}: {Message}", key, ex.Message);
                return false;
            }
        }

        public async Task<bool> KeyExistsAsync(string key)
        {
            try
            {
                return await _database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error checking Redis key {Key}: {Message}", key, ex.Message);
                return false;
            }
        }

        public async Task<bool> KeyExpireAsync(string key, TimeSpan expiry)
        {
            try
            {
                return await _database.KeyExpireAsync(key, expiry);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error setting expiration for Redis key {Key}: {Message}", key, ex.Message);
                return false;
            }
        }

        public async Task<long> IncrementAsync(string key)
        {
            try
            {
                return await _database.StringIncrementAsync(key);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error incrementing Redis key {Key}: {Message}", key, ex.Message);
                return 0;
            }
        }

        public void Dispose()
        {
            _redis?.Dispose();
        }
    }
}