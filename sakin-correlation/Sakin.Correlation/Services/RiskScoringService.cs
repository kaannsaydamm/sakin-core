using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Models;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Models;

namespace Sakin.Correlation.Services;

public interface IRiskScoringService
{
    RiskScore CalculateRisk(AlertEntity alert, EventEnvelope envelope);
}

public class RiskScoringService : IRiskScoringService
{
    private readonly ITimeOfDayService _timeOfDayService;
    private readonly IUserRiskProfileService _userRiskProfileService;
    private readonly ILogger<RiskScoringService> _logger;
    private readonly RiskScoringConfiguration _config;

    public RiskScoringService(
        ITimeOfDayService timeOfDayService,
        IUserRiskProfileService userRiskProfileService,
        ILogger<RiskScoringService> logger,
        IOptions<RiskScoringConfiguration> config)
    {
        _timeOfDayService = timeOfDayService;
        _userRiskProfileService = userRiskProfileService;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<RiskScore> CalculateRiskAsync(AlertEntity alert, EventEnvelope envelope)
    {
        var factors = new Dictionary<string, double>();

        // 1. Base severity score (20-100)
        var baseScore = _config.Factors.BaseWeights.GetValueOrDefault(alert.Severity.ToString(), 50);
        factors["base_severity"] = baseScore;

        // 2. Asset criticality multiplier and boost
        var asset = ExtractAssetFromEnvelope(envelope);
        var assetMultiplier = _config.Factors.AssetMultipliers.GetValueOrDefault(
            asset?.Criticality.ToString() ?? "Low", 1.0);
        var assetBoost = (baseScore * assetMultiplier) - baseScore; // Only the additional points
        factors["asset_boost"] = assetBoost;

        // 3. Threat intel boost (0-30)
        var threatIntelScore = ExtractThreatIntelFromEnvelope(envelope);
        var tiBoost = threatIntelScore?.IsKnownMalicious == true 
            ? (double)threatIntelScore.Score / 100.0 * _config.Factors.ThreatIntelMaxBoost 
            : 0;
        factors["threat_intel_boost"] = tiBoost;

        // 4. Time of day multiplier
        var isOffHours = _timeOfDayService.IsOffHours(alert.TriggeredAt);
        var timeMultiplier = isOffHours ? _config.Factors.OffHoursMultiplier : 1.0;
        factors["time_of_day_multiplier"] = timeMultiplier;

        // 5. User risk profile boost (0-50)
        var username = envelope.Normalized?.Username;
        var userRiskBoost = !string.IsNullOrWhiteSpace(username) 
            ? await _userRiskProfileService.GetUserRiskScore(username)
            : 0;
        factors["user_risk_boost"] = userRiskBoost;

        // 6. Anomaly score boost (0-20)
        var anomalyBoost = ExtractAnomalyScoreFromEnvelope(envelope);
        factors["anomaly_boost"] = anomalyBoost;

        // Calculate final score using the weighted formula
        var finalScore = (baseScore + assetBoost + tiBoost + userRiskBoost + anomalyBoost) * timeMultiplier;
        finalScore = Math.Min(100.0, Math.Max(0.0, finalScore)); // Clamp to 0-100

        var riskScore = new RiskScore
        {
            Score = (int)Math.Round(finalScore),
            Level = GetRiskLevelFromScore((int)Math.Round(finalScore)),
            Factors = factors,
            Reasoning = GenerateReasoning(factors, asset, threatIntelScore, isOffHours, username)
        };

        _logger.LogDebug("Calculated risk score {Score} ({Level}) for alert {AlertId}", 
            riskScore.Score, riskScore.Level, alert.Id);

        return riskScore;
    }

    public RiskScore CalculateRisk(AlertEntity alert, EventEnvelope envelope)
    {
        // Synchronous version for compatibility - run async method synchronously
        return CalculateRiskAsync(alert, envelope).GetAwaiter().GetResult();
    }

    private static Asset? ExtractAssetFromEnvelope(EventEnvelope envelope)
    {
        // Try to get source asset from enrichment
        if (envelope.Enrichment.TryGetValue("source_asset", out var sourceAssetObj) && 
            sourceAssetObj is JsonElement sourceAssetElement)
        {
            return ParseAssetFromJsonElement(sourceAssetElement);
        }

        // Try destination asset
        if (envelope.Enrichment.TryGetValue("destination_asset", out var destAssetObj) && 
            destAssetObj is JsonElement destAssetElement)
        {
            return ParseAssetFromJsonElement(destAssetElement);
        }

        return null;
    }

    private static Asset? ParseAssetFromJsonElement(JsonElement element)
    {
        try
        {
            var asset = new Asset();
            
            if (element.TryGetProperty("name", out var nameProp))
                asset.Name = nameProp.GetString() ?? string.Empty;
            
            if (element.TryGetProperty("criticality", out var criticalityProp))
            {
                var criticalityStr = criticalityProp.GetString();
                if (!string.IsNullOrWhiteSpace(criticalityStr) && 
                    Enum.TryParse<AssetCriticality>(criticalityStr, true, out var criticality))
                {
                    asset.Criticality = criticality;
                }
            }
            
            return asset;
        }
        catch
        {
            return null;
        }
    }

    private static ThreatIntelScore? ExtractThreatIntelFromEnvelope(EventEnvelope envelope)
    {
        // Look for threat intel in enrichment
        foreach (var enrichment in envelope.Enrichment.Values)
        {
            if (enrichment is JsonElement element && element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("is_malicious", out _) && 
                    element.TryGetProperty("score", out _))
                {
                    try
                    {
                        var tiScore = element.Deserialize<ThreatIntelScore>();
                        return tiScore;
                    }
                    catch
                    {
                        // Continue to next enrichment
                    }
                }
            }
        }

        return null;
    }

    private static double ExtractAnomalyScoreFromEnvelope(EventEnvelope envelope)
    {
        // Look for anomaly score in enrichment
        if (envelope.Enrichment.TryGetValue("anomaly_score", out var anomalyObj))
        {
            if (anomalyObj is JsonElement anomalyElement && anomalyElement.TryGetDouble(out var anomalyScore))
            {
                return anomalyScore;
            }
            if (double.TryParse(anomalyObj?.ToString(), out var score))
            {
                return score;
            }
        }

        return 0;
    }

    private static RiskLevel GetRiskLevelFromScore(int score)
    {
        return score switch
        {
            >= 80 => RiskLevel.Critical,
            >= 60 => RiskLevel.High,
            >= 40 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };
    }

    private string GenerateReasoning(
        Dictionary<string, double> factors, 
        Asset? asset, 
        ThreatIntelScore? threatIntel, 
        bool isOffHours, 
        string? username)
    {
        var reasons = new List<string>();
        
        // Base severity
        if (factors.TryGetValue("base_severity", out var baseScore))
        {
            reasons.Add($"base severity score of {baseScore}");
        }

        // Asset criticality
        if (factors.TryGetValue("asset_boost", out var assetBoost) && assetBoost > 0 && asset != null)
        {
            reasons.Add($"{asset.Criticality} criticality asset ({asset.Name})");
        }

        // Threat intel
        if (factors.TryGetValue("threat_intel_boost", out var tiBoost) && tiBoost > 0 && threatIntel?.IsKnownMalicious == true)
        {
            reasons.Add($"known malicious IP (threat intel score: {threatIntel.Score})");
        }

        // Time of day
        if (factors.TryGetValue("time_of_day_multiplier", out var timeMultiplier) && timeMultiplier > 1.0)
        {
            reasons.Add("occurred outside business hours");
        }

        // User risk
        if (factors.TryGetValue("user_risk_boost", out var userRisk) && userRisk > 0 && !string.IsNullOrWhiteSpace(username))
        {
            reasons.Add($"high-risk user ({username})");
        }

        // Anomaly
        if (factors.TryGetValue("anomaly_boost", out var anomalyBoost) && anomalyBoost > 0)
        {
            reasons.Add($"anomalous activity detected");
        }

        if (reasons.Count == 0)
        {
            return "Risk calculated based on base factors.";
        }

        return $"Risk score is HIGH due to: {string.Join(", ", reasons)}";
    }
}