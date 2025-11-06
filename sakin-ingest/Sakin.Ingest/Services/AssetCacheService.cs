using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Sakin.Common.Cache;
using Sakin.Common.Models;

namespace Sakin.Ingest.Services;

public interface IAssetCacheService
{
    Asset? GetAsset(string ipOrHostname);
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public class AssetCacheService : IAssetCacheService, IHostedService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IRedisClient _redisClient;
    private readonly ILogger<AssetCacheService> _logger;
    private readonly string _connectionString;
    private readonly Timer _refreshTimer;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
    private IDisposable? _redisSubscription;

    private const string AssetCacheKeyPrefix = "asset:";
    private const string AssetIpKeyPrefix = "asset:ip:";
    private const string AssetHostnameKeyPrefix = "asset:host:";
    private const string RedisChannel = "sakin:cache:notify";

    public AssetCacheService(
        IMemoryCache memoryCache,
        IRedisClient redisClient,
        IConfiguration configuration,
        ILogger<AssetCacheService> logger)
    {
        _memoryCache = memoryCache;
        _redisClient = redisClient;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("Postgres connection string not configured");
        
        // Refresh cache every 5 minutes as a fallback
        _refreshTimer = new Timer(RefreshCacheFromDatabase, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Asset Cache Service");
        
        // Initial load of assets from database
        await RefreshCacheFromDatabase(null);
        
        // Subscribe to Redis pub/sub for real-time updates
        try
        {
            await _redisClient.SubscribeAsync(RedisChannel, HandleCacheInvalidation);
            _logger.LogInformation("Subscribed to asset cache invalidation channel");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to Redis pub/sub channel");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Asset Cache Service");
        
        _refreshTimer?.Change(Timeout.Infinite, 0);
        _redisSubscription?.Dispose();
        _refreshSemaphore.Dispose();
    }

    public Asset? GetAsset(string ipOrHostname)
    {
        if (string.IsNullOrWhiteSpace(ipOrHostname))
            return null;

        // Try IP lookup first
        if (IsValidIpAddress(ipOrHostname))
        {
            var ipKey = $"{AssetIpKeyPrefix}{ipOrHostname}";
            if (_memoryCache.TryGetValue(ipKey, out Asset? ipAsset))
            {
                return ipAsset;
            }
        }

        // Try hostname lookup
        var hostnameKey = $"{AssetHostnameKeyPrefix}{ipOrHostname.ToLowerInvariant()}";
        if (_memoryCache.TryGetValue(hostnameKey, out Asset? hostnameAsset))
        {
            return hostnameAsset;
        }

        return null;
    }

    private async void RefreshCacheFromDatabase(object? state)
    {
        if (!await _refreshSemaphore.WaitAsync(TimeSpan.FromMinutes(1)))
        {
            _logger.LogWarning("Could not acquire semaphore for cache refresh - another refresh is in progress");
            return;
        }

        try
        {
            _logger.LogDebug("Refreshing asset cache from database");
            
            using var connection = new Npgsql.NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT id, name, ip_address, hostname, asset_type, criticality, owner, tags, description, created_at, updated_at
                FROM Assets 
                ORDER BY updated_at DESC";

            var assets = await connection.QueryAsync<Asset>(sql);
            
            // Clear existing cache
            _memoryCache.Remove(AssetCacheKeyPrefix);

            // Populate cache with fresh data
            foreach (var asset in assets)
            {
                CacheAsset(asset);
            }

            _logger.LogDebug("Asset cache refreshed with {Count} assets", assets.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing asset cache from database");
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }

    private async void HandleCacheInvalidation(string message)
    {
        try
        {
            _logger.LogDebug("Received cache invalidation message: {Message}", message);
            
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            
            if (!root.TryGetProperty("action", out var actionProperty))
                return;

            var action = actionProperty.GetString();
            
            switch (action)
            {
                case "asset_updated":
                case "asset_deleted":
                    await HandleAssetUpdate(root);
                    break;
                    
                case "batch_update":
                    // For batch updates, do a full refresh
                    RefreshCacheFromDatabase(null);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling cache invalidation message: {Message}", message);
        }
    }

    private async Task HandleAssetUpdate(JsonElement message)
    {
        try
        {
            var ipAddress = message.GetPropertyOrNull("ip_address")?.GetString();
            var hostname = message.GetPropertyOrNull("hostname")?.GetString();
            var action = message.GetProperty("action").GetString();

            if (action == "asset_deleted")
            {
                // Remove from cache
                if (!string.IsNullOrWhiteSpace(ipAddress))
                {
                    _memoryCache.Remove($"{AssetIpKeyPrefix}{ipAddress}");
                }
                
                if (!string.IsNullOrWhiteSpace(hostname))
                {
                    _memoryCache.Remove($"{AssetHostnameKeyPrefix}{hostname.ToLowerInvariant()}");
                }
                
                _logger.LogDebug("Removed asset from cache: IP={IP}, Hostname={Hostname}", ipAddress, hostname);
            }
            else
            {
                // For updates, we could either refresh the specific asset or do a full refresh
                // For simplicity, do a full refresh for now
                RefreshCacheFromDatabase(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling asset update");
        }
    }

    private void CacheAsset(Asset asset)
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(30),
            Priority = CacheItemPriority.Normal
        };

        // Cache by IP address if available
        if (!string.IsNullOrWhiteSpace(asset.IpAddress))
        {
            var ipKey = $"{AssetIpKeyPrefix}{asset.IpAddress}";
            _memoryCache.Set(ipKey, asset, cacheOptions);
        }

        // Cache by hostname if available
        if (!string.IsNullOrWhiteSpace(asset.Hostname))
        {
            var hostnameKey = $"{AssetHostnameKeyPrefix}{asset.Hostname.ToLowerInvariant()}";
            _memoryCache.Set(hostnameKey, asset, cacheOptions);
        }
    }

    private static bool IsValidIpAddress(string ip)
    {
        return System.Net.IPAddress.TryParse(ip, out _);
    }
}

// Extension method for JsonElement to safely get properties
internal static class JsonElementExtensions
{
    public static JsonElement? GetPropertyOrNull(this JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? value : null;
    }
}