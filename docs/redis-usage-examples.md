# Redis Client Usage Examples

This document provides examples of using the Redis client wrapper in the SAKIN project.

## Basic Usage

### 1. Configuration

Add Redis configuration to your `appsettings.json`:

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

### 2. Dependency Injection Setup

In your `Program.cs`:

```csharp
using Sakin.Common.Configuration;
using Sakin.Common.DependencyInjection;

// Configure services
services.Configure<RedisOptions>(
    configuration.GetSection(RedisOptions.SectionName));
services.AddRedisClient();
```

### 3. Using the Redis Client

```csharp
using Sakin.Common.Cache;

public class EventCacheService
{
    private readonly IRedisClient _redis;
    private readonly ILogger<EventCacheService> _logger;

    public EventCacheService(IRedisClient redis, ILogger<EventCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task CacheEventAsync(string eventId, string eventData, TimeSpan expiry)
    {
        try
        {
            await _redis.StringSetAsync($"event:{eventId}", eventData, expiry);
            _logger.LogInformation("Cached event {EventId}", eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache event {EventId}", eventId);
        }
    }

    public async Task<string?> GetCachedEventAsync(string eventId)
    {
        try
        {
            return await _redis.StringGetAsync($"event:{eventId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached event {EventId}", eventId);
            return null;
        }
    }

    public async Task<bool> IsEventCachedAsync(string eventId)
    {
        return await _redis.KeyExistsAsync($"event:{eventId}");
    }

    public async Task RemoveCachedEventAsync(string eventId)
    {
        await _redis.KeyDeleteAsync($"event:{eventId}");
    }

    public async Task<long> IncrementEventCountAsync(string eventType)
    {
        return await _redis.IncrementAsync($"count:{eventType}");
    }
}
```

## Advanced Usage Examples

### Rate Limiting

```csharp
public class RateLimiter
{
    private readonly IRedisClient _redis;

    public RateLimiter(IRedisClient redis)
    {
        _redis = redis;
    }

    public async Task<bool> IsAllowedAsync(string clientId, int limit, TimeSpan window)
    {
        string key = $"rate_limit:{clientId}";
        var current = await _redis.IncrementAsync(key);
        
        if (current == 1)
        {
            // Set expiry on first request
            await _redis.StringSetAsync(key, current.ToString(), window);
        }
        
        return current <= limit;
    }
}
```

### Session Management

```csharp
public class SessionManager
{
    private readonly IRedisClient _redis;

    public SessionManager(IRedisClient redis)
    {
        _redis = redis;
    }

    public async Task CreateSessionAsync(string sessionId, string userId, TimeSpan expiry)
    {
        string sessionData = $"{{\"userId\":\"{userId}\",\"createdAt\":\"{DateTime.UtcNow:O}\"}}";
        await _redis.StringSetAsync($"session:{sessionId}", sessionData, expiry);
    }

    public async Task<string?> GetUserIdAsync(string sessionId)
    {
        string? sessionData = await _redis.StringGetAsync($"session:{sessionId}");
        if (string.IsNullOrEmpty(sessionData)) return null;

        // Simple parsing - in real implementation, use JSON deserialization
        var parts = sessionData.Split("\"userId\":\"");
        if (parts.Length > 1)
        {
            var userId = parts[1].Split("\"")[0];
            return userId;
        }
        return null;
    }

    public async Task<bool> IsValidSessionAsync(string sessionId)
    {
        return await _redis.KeyExistsAsync($"session:{sessionId}");
    }
}
```

## Configuration Options

### Connection String Formats

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

```json
{
  "Redis": {
    "ConnectionString": "redis.example.com:6379,password=mypassword,ssl=true,abortConnect=false"
  }
}
```

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379,localhost:6380,localhost:6381"
  }
}
```

### Environment Variable Override

```bash
# Override the connection string
export Redis__ConnectionString="redis-cluster.example.com:6379"

# Or in Docker compose
environment:
  - Redis__ConnectionString=redis:6379
```

## Error Handling

The Redis client wrapper includes basic error handling:

- All methods return default values on failure (null for string operations, false for boolean operations, 0 for increment)
- Errors are logged through the provided ILogger
- Connection errors are thrown during construction

For more sophisticated error handling, catch exceptions in your application code and implement retry logic as needed.