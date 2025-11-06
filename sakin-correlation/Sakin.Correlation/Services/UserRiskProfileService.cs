using Microsoft.Extensions.Logging;
using Sakin.Common.Cache;
using Sakin.Common.Models;

namespace Sakin.Correlation.Services;

public interface IUserRiskProfileService
{
    Task<double> GetUserRiskScore(string username);
    Task UpdateUserRiskProfileAsync(NormalizedEvent normalizedEvent);
}

public class UserRiskProfileService : IUserRiskProfileService
{
    private readonly IRedisClient _redisClient;
    private readonly ILogger<UserRiskProfileService> _logger;
    private const string UserRiskKeyPrefix = "sakin:user_risk:";
    private const int RiskWindowDays = 7;
    private const int MaxRiskScore = 50;

    public UserRiskProfileService(
        IRedisClient redisClient,
        ILogger<UserRiskProfileService> logger)
    {
        _redisClient = redisClient;
        _logger = logger;
    }

    public async Task<double> GetUserRiskScore(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return 0;
        }

        try
        {
            var riskKey = $"{UserRiskKeyPrefix}{username}";
            var riskData = await _redisClient.GetAsync<Dictionary<string, object>>(riskKey);
            
            if (riskData == null)
            {
                return 0;
            }

            var score = 0.0;
            if (riskData.TryGetValue("score", out var scoreObj) && scoreObj is double scoreValue)
            {
                score = scoreValue;
            }

            _logger.LogDebug("User {Username} risk score: {Score}", username, score);
            return Math.Min(score, MaxRiskScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user risk score for {Username}", username);
            return 0;
        }
    }

    public async Task UpdateUserRiskProfileAsync(NormalizedEvent normalizedEvent)
    {
        if (normalizedEvent == null || string.IsNullOrWhiteSpace(normalizedEvent.Username))
        {
            return;
        }

        try
        {
            var username = normalizedEvent.Username;
            var riskKey = $"{UserRiskKeyPrefix}{username}";
            
            // Get existing risk data
            var riskData = await _redisClient.GetAsync<Dictionary<string, object>>(riskKey) ?? new Dictionary<string, object>();
            
            var currentScore = 0.0;
            if (riskData.TryGetValue("score", out var scoreObj) && scoreObj is double scoreValue)
            {
                currentScore = scoreValue;
            }

            var riskIncrease = CalculateRiskIncrease(normalizedEvent);
            var newScore = Math.Min(currentScore + riskIncrease, MaxRiskScore);
            
            // Update risk data
            riskData["score"] = newScore;
            riskData["last_updated"] = DateTimeOffset.UtcNow;
            riskData["last_event_type"] = normalizedEvent.EventType;
            riskData["last_event_timestamp"] = normalizedEvent.Timestamp;

            // Store with 7-day TTL
            await _redisClient.SetAsync(riskKey, riskData, TimeSpan.FromDays(RiskWindowDays));
            
            _logger.LogDebug("Updated user {Username} risk score from {OldScore} to {NewScore} due to event {EventType}", 
                username, currentScore, newScore, normalizedEvent.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user risk profile for {Username}", normalizedEvent.Username);
        }
    }

    private double CalculateRiskIncrease(NormalizedEvent normalizedEvent)
    {
        return normalizedEvent.EventType.ToLowerInvariant() switch
        {
            "auth.failed" => 5.0,
            "auth.lockout" => 10.0,
            "auth.privilege_escalation" => 15.0,
            "auth.unusual_time" => 8.0,
            "auth.unusual_location" => 12.0,
            "data.access_sensitive" => 7.0,
            "data.exfiltration_attempt" => 20.0,
            "malware.detected" => 25.0,
            "policy.violation" => 10.0,
            "network.suspicious_traffic" => 8.0,
            "network.command_and_control" => 30.0,
            _ => 1.0 // Small increase for any other event
        };
    }
}