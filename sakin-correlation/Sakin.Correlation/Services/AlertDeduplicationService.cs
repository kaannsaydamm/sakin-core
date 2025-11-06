using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sakin.Common.Cache;
using Sakin.Common.Models;

namespace Sakin.Correlation.Services;

public interface IAlertDeduplicationService
{
    Task<string> GenerateDedupKeyAsync(string ruleId, EventEnvelope eventEnvelope, CancellationToken cancellationToken = default);
    
    Task<bool> IsAlertDuplicateAsync(string dedupKey, TimeSpan ttl, CancellationToken cancellationToken = default);
    
    Task StoreDeduplicationKeyAsync(string dedupKey, Guid alertId, TimeSpan ttl, CancellationToken cancellationToken = default);
    
    Task<Guid?> GetDuplicateAlertIdAsync(string dedupKey, CancellationToken cancellationToken = default);
}

public class AlertDeduplicationService : IAlertDeduplicationService
{
    private readonly IRedisClient _redisClient;
    private readonly ILogger<AlertDeduplicationService> _logger;
    private const string DeduplicationKeyPrefix = "dedup:";

    public AlertDeduplicationService(
        IRedisClient redisClient,
        ILogger<AlertDeduplicationService> logger)
    {
        _redisClient = redisClient;
        _logger = logger;
    }

    public async Task<string> GenerateDedupKeyAsync(
        string ruleId,
        EventEnvelope eventEnvelope,
        CancellationToken cancellationToken = default)
    {
        var sourceIp = ExtractIp(eventEnvelope.Source);
        var destIp = ExtractDestinationIp(eventEnvelope);

        var keyMaterial = $"{ruleId}:{sourceIp}:{destIp}";
        var hash = ComputeSha256(keyMaterial);

        return $"{DeduplicationKeyPrefix}{hash}";
    }

    public async Task<bool> IsAlertDuplicateAsync(
        string dedupKey,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existingId = await _redisClient.GetStringAsync(dedupKey, cancellationToken);
            return !string.IsNullOrEmpty(existingId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking deduplication for key {DedupKey}", dedupKey);
            return false;
        }
    }

    public async Task StoreDeduplicationKeyAsync(
        string dedupKey,
        Guid alertId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _redisClient.SetStringAsync(dedupKey, alertId.ToString(), ttl, cancellationToken);
            _logger.LogDebug("Stored deduplication key {DedupKey} for alert {AlertId} with TTL {TtlSeconds}s", 
                dedupKey, alertId, ttl.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing deduplication key {DedupKey}", dedupKey);
        }
    }

    public async Task<Guid?> GetDuplicateAlertIdAsync(
        string dedupKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var idString = await _redisClient.GetStringAsync(dedupKey, cancellationToken);
            if (string.IsNullOrEmpty(idString) || !Guid.TryParse(idString, out var alertId))
            {
                return null;
            }
            return alertId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving duplicate alert ID for key {DedupKey}", dedupKey);
            return null;
        }
    }

    private static string ExtractIp(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return "unknown";
        }

        var parts = source.Split(':');
        return parts.Length > 0 ? parts[0] : "unknown";
    }

    private static string ExtractDestinationIp(EventEnvelope envelope)
    {
        if (envelope.Normalized is not null && 
            envelope.Normalized.TryGetValue("destination_ip", out var destIp) && 
            destIp is string destIpStr)
        {
            return destIpStr;
        }

        return "unknown";
    }

    private static string ComputeSha256(string input)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashedBytes).ToLowerInvariant();
    }
}
