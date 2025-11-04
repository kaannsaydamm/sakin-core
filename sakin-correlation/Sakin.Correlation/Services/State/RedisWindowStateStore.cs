using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Models;
using Sakin.Common.Models;
using StackExchange.Redis;

namespace Sakin.Correlation.Services.State;

public class RedisWindowStateStore : IWindowStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerOptions.Default)
    {
        WriteIndented = false
    };

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisWindowStateStore> _logger;
    private readonly ConcurrentDictionary<string, string> _groupKeyCache = new();

    public RedisWindowStateStore(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisSettings> options,
        ILogger<RedisWindowStateStore> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger;

        var redisSettings = options.Value;
        if (string.IsNullOrWhiteSpace(redisSettings.ConnectionString))
        {
            throw new ArgumentException("Redis connection string is not configured", nameof(options));
        }
    }

    public async Task<IReadOnlyList<EventEnvelope>> AddEventAsync(
        CorrelationRule rule,
        string groupKey,
        EventEnvelope envelope,
        TimeSpan windowSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = envelope.Normalized;
        if (normalized is null)
        {
            return Array.Empty<EventEnvelope>();
        }

        var timestamp = normalized.Timestamp == default
            ? DateTime.UtcNow
            : normalized.Timestamp;

        var db = _connectionMultiplexer.GetDatabase();
        var keySegment = BuildKeySegment(rule.Id, groupKey);
        var sortedSetKey = RedisKey(op: $"rule:{keySegment}:events");
        var payloadKey = RedisKey(op: $"rule:{keySegment}:event:{envelope.EventId}");

        var score = new DateTimeOffset(timestamp).ToUnixTimeMilliseconds();
        var windowStart = new DateTimeOffset(timestamp - windowSize).ToUnixTimeMilliseconds();
        var expiry = windowSize + windowSize; // keep events for two window lengths

        await db.SortedSetAddAsync(sortedSetKey, envelope.EventId.ToString(), score).ConfigureAwait(false);
        await db.KeyExpireAsync(sortedSetKey, expiry).ConfigureAwait(false);

        var serialized = JsonSerializer.Serialize(envelope, SerializerOptions);
        await db.StringSetAsync(payloadKey, serialized, expiry).ConfigureAwait(false);

        await RemoveExpiredEventsAsync(db, keySegment, sortedSetKey, windowStart).ConfigureAwait(false);

        var eventIds = await db.SortedSetRangeByScoreAsync(sortedSetKey, windowStart, double.PositiveInfinity)
            .ConfigureAwait(false);

        if (eventIds.Length == 0)
        {
            return Array.Empty<EventEnvelope>();
        }

        var events = new List<EventEnvelope>(eventIds.Length);
        foreach (var id in eventIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var eventKey = RedisKey(op: $"rule:{keySegment}:event:{id}");
            var payload = await db.StringGetAsync(eventKey).ConfigureAwait(false);
            if (!payload.HasValue)
            {
                continue;
            }

            try
            {
                var deserialized = JsonSerializer.Deserialize<EventEnvelope>(payload!, SerializerOptions);
                if (deserialized != null)
                {
                    events.Add(deserialized);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize event payload for rule {RuleId}", rule.Id);
                await db.KeyDeleteAsync(eventKey).ConfigureAwait(false);
            }
        }

        return events
            .OrderBy(e => e.Normalized!.Timestamp)
            .ToList();
    }

    private async Task RemoveExpiredEventsAsync(IDatabase db, string keySegment, RedisKey sortedSetKey, long windowStart)
    {
        var expired = await db.SortedSetRangeByScoreAsync(sortedSetKey, double.NegativeInfinity, windowStart - 1)
            .ConfigureAwait(false);

        if (expired.Length == 0)
        {
            return;
        }

        await db.SortedSetRemoveRangeByScoreAsync(sortedSetKey, double.NegativeInfinity, windowStart - 1)
            .ConfigureAwait(false);

        foreach (var id in expired)
        {
            var payloadKey = RedisKey(op: $"rule:{keySegment}:event:{id}");
            await db.KeyDeleteAsync(payloadKey).ConfigureAwait(false);
        }
    }

    private string BuildKeySegment(string ruleId, string groupKey)
    {
        var normalizedRuleId = Sanitize(ruleId);
        var normalizedGroup = string.IsNullOrWhiteSpace(groupKey)
            ? "global"
            : _groupKeyCache.GetOrAdd(groupKey, static value =>
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
                return Convert.ToHexString(hash).ToLowerInvariant();
            });

        return $"{normalizedRuleId}:group:{normalizedGroup}";
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('-');
            }
        }
        return builder.ToString().ToLowerInvariant();
    }

    private static RedisKey RedisKey(string op) => (RedisKey)op;
}
