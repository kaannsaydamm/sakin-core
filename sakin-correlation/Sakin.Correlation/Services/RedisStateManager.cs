using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sakin.Common.Cache;
using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services;

public class RedisStateManager : IStateManager
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IRedisClient _redisClient;
    private readonly ILogger<RedisStateManager> _logger;

    public RedisStateManager(IRedisClient redisClient, ILogger<RedisStateManager> logger)
    {
        _redisClient = redisClient;
        _logger = logger;
    }

    public async Task<CorrelationState> AddEventAsync(string ruleId, string groupKey, Guid eventId, TimeSpan expiration, CancellationToken cancellationToken)
    {
        var redisKey = BuildRedisKey(ruleId, groupKey);

        var existingJson = await _redisClient.StringGetAsync(redisKey);
        CorrelationState state;

        if (string.IsNullOrWhiteSpace(existingJson))
        {
            state = new CorrelationState
            {
                EventCount = 1,
                EventIds = new List<Guid> { eventId }
            };
            await _redisClient.StringSetAsync(redisKey, JsonSerializer.Serialize(state, SerializerOptions), expiration);
            _logger.LogDebug("Created new correlation state for rule {RuleId} with key {GroupKey}", ruleId, groupKey);
        }
        else
        {
            state = JsonSerializer.Deserialize<CorrelationState>(existingJson, SerializerOptions) ?? new CorrelationState();
            state.EventIds.Add(eventId);
            state = state with { EventCount = state.EventIds.Count };
            await _redisClient.StringSetAsync(redisKey, JsonSerializer.Serialize(state, SerializerOptions), expiration);
        }

        return state;
    }

    public async Task<CorrelationState?> GetStateAsync(string ruleId, string groupKey, CancellationToken cancellationToken)
    {
        var redisKey = BuildRedisKey(ruleId, groupKey);
        var json = await _redisClient.StringGetAsync(redisKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<CorrelationState>(json, SerializerOptions);
    }

    public async Task ClearGroupAsync(string ruleId, string groupKey, CancellationToken cancellationToken)
    {
        var redisKey = BuildRedisKey(ruleId, groupKey);
        await _redisClient.KeyDeleteAsync(redisKey);
        _logger.LogDebug("Cleared correlation state for rule {RuleId} and key {GroupKey}", ruleId, groupKey);
    }

    private static string BuildRedisKey(string ruleId, string groupKey) => $"correlation:{ruleId}:{groupKey}";
}
