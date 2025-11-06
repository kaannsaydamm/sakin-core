using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sakin.Common.Models;
using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Models;
using Sakin.Correlation.Persistence.Repositories;
using Sakin.Correlation.Persistence.Entities;

namespace Sakin.Correlation.Services;

public class AlertCreatorService : IAlertCreatorService, IAlertCreatorServiceWithRiskScoring
{
    private readonly IAlertRepository _alertRepository;
    private readonly IAssetCacheService _assetCacheService;
    private readonly ILogger<AlertCreatorService> _logger;
    private readonly IMetricsService? _metricsService;
    private readonly RiskScoringWorker? _riskScoringWorker;
    private readonly IAlertActionPublisher? _alertActionPublisher;

    public AlertCreatorService(
        IAlertRepository alertRepository,
        IAssetCacheService assetCacheService,
        ILogger<AlertCreatorService> logger,
        IMetricsService? metricsService = null,
        RiskScoringWorker? riskScoringWorker = null,
        IAlertActionPublisher? alertActionPublisher = null)
    {
        _alertRepository = alertRepository;
        _assetCacheService = assetCacheService;
        _logger = logger;
        _metricsService = metricsService;
        _riskScoringWorker = riskScoringWorker;
        _alertActionPublisher = alertActionPublisher;
    }

    public async Task CreateAlertAsync(CorrelationRule rule, EventEnvelope eventEnvelope, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating alert for rule {RuleId} - {RuleName}", rule.Id, rule.Name);

            // Determine asset context and boost severity if needed
            var (adjustedSeverity, assetContext) = await DetermineSeverityWithAssetContext(eventEnvelope, rule.Severity);

            var alertRecord = new AlertRecord
            {
                Id = Guid.NewGuid(),
                RuleId = rule.Id,
                RuleName = rule.Name,
                Severity = adjustedSeverity,
                Status = AlertStatus.New,
                TriggeredAt = DateTimeOffset.UtcNow,
                Source = eventEnvelope.Source,
                Context = BuildContext(eventEnvelope, assetContext),
                MatchedConditions = Array.Empty<string>(), // Will be populated by rule evaluator
                AggregationCount = null, // Stateless rules don't have aggregation
                AggregatedValue = null, // Stateless rules don't have aggregation
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var createdAlert = await _alertRepository.CreateAsync(alertRecord, cancellationToken);
            
            _metricsService?.IncrementAlertsCreated();
            
            _logger.LogInformation(
                "Alert created successfully: {AlertId} for rule {RuleId} with severity {Severity} (original: {OriginalSeverity})", 
                createdAlert.Id, rule.Id, adjustedSeverity, rule.Severity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create alert for rule {RuleId}", rule.Id);
            throw;
        }
    }

    public async Task CreateAlertWithRiskScoringAsync(CorrelationRule rule, EventEnvelope eventEnvelope, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating alert with risk scoring for rule {RuleId} - {RuleName}", rule.Id, rule.Name);

            // Determine asset context and boost severity if needed
            var (adjustedSeverity, assetContext) = await DetermineSeverityWithAssetContext(eventEnvelope, rule.Severity);

            var alertRecord = new AlertRecord
            {
                Id = Guid.NewGuid(),
                RuleId = rule.Id,
                RuleName = rule.Name,
                Severity = adjustedSeverity,
                Status = AlertStatus.New,
                TriggeredAt = DateTimeOffset.UtcNow,
                Source = eventEnvelope.Source,
                Context = BuildContext(eventEnvelope, assetContext),
                MatchedConditions = Array.Empty<string>(), // Will be populated by rule evaluator
                AggregationCount = null, // Stateless rules don't have aggregation
                AggregatedValue = null, // Stateless rules don't have aggregation
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var createdAlert = await _alertRepository.CreateAsync(alertRecord, cancellationToken);
            
            // Queue for risk scoring if the worker is available
            if (_riskScoringWorker != null)
            {
                // Convert AlertRecord to AlertEntity for risk scoring
                var alertEntity = ConvertToAlertEntity(createdAlert, eventEnvelope);
                await _riskScoringWorker.QueueRiskScoringAsync(createdAlert.Id, alertEntity, eventEnvelope);
                
                _logger.LogInformation("Alert {AlertId} queued for risk scoring", createdAlert.Id);
            }
            else
            {
                _logger.LogWarning("RiskScoringWorker not available, alert {AlertId} created without risk scoring", createdAlert.Id);
            }
            
            _metricsService?.IncrementAlertsCreated();
            
            _logger.LogInformation(
                "Alert created successfully: {AlertId} for rule {RuleId} with severity {Severity} (original: {OriginalSeverity})", 
                createdAlert.Id, rule.Id, adjustedSeverity, rule.Severity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create alert for rule {RuleId}", rule.Id);
            throw;
        }
    }

    private static AlertEntity ConvertToAlertEntity(AlertRecord alertRecord, EventEnvelope eventEnvelope)
    {
        return new AlertEntity
        {
            Id = alertRecord.Id,
            RuleId = alertRecord.RuleId,
            RuleName = alertRecord.RuleName,
            Severity = alertRecord.Severity.ToString().ToLowerInvariant(),
            Status = "pending_score", // Special status for pending risk scoring
            TriggeredAt = alertRecord.TriggeredAt,
            Source = alertRecord.Source,
            CorrelationContext = JsonSerializer.Serialize(alertRecord.Context),
            MatchedConditions = JsonSerializer.Serialize(alertRecord.MatchedConditions),
            AggregationCount = alertRecord.AggregationCount,
            AggregatedValue = alertRecord.AggregatedValue,
            CreatedAt = alertRecord.CreatedAt,
            UpdatedAt = alertRecord.UpdatedAt
        };
    }

    private static Dictionary<string, object?> BuildContext(EventEnvelope eventEnvelope)
    {
        return BuildContext(eventEnvelope, null);
    }

    private Dictionary<string, object?> BuildContext(EventEnvelope eventEnvelope, Dictionary<string, object>? assetContext)
    {
        var context = new Dictionary<string, object?>
        {
            ["eventId"] = eventEnvelope.EventId,
            ["source"] = eventEnvelope.Source,
            ["sourceType"] = eventEnvelope.SourceType,
            ["receivedAt"] = eventEnvelope.ReceivedAt,
            ["raw"] = eventEnvelope.Raw
        };

        if (eventEnvelope.Normalized != null)
        {
            context["normalized"] = new Dictionary<string, object?>
            {
                ["id"] = eventEnvelope.Normalized.Id,
                ["timestamp"] = eventEnvelope.Normalized.Timestamp,
                ["eventType"] = eventEnvelope.Normalized.EventType,
                ["severity"] = eventEnvelope.Normalized.Severity,
                ["sourceIp"] = eventEnvelope.Normalized.SourceIp,
                ["destinationIp"] = eventEnvelope.Normalized.DestinationIp,
                ["sourcePort"] = eventEnvelope.Normalized.SourcePort,
                ["destinationPort"] = eventEnvelope.Normalized.DestinationPort,
                ["protocol"] = eventEnvelope.Normalized.Protocol,
                ["payload"] = eventEnvelope.Normalized.Payload,
                ["deviceName"] = eventEnvelope.Normalized.DeviceName,
                ["sensorId"] = eventEnvelope.Normalized.SensorId
            };

            if (eventEnvelope.Normalized.Metadata.Count > 0)
            {
                ((Dictionary<string, object?>)context["normalized"]!)["metadata"] = eventEnvelope.Normalized.Metadata;
            }
        }

        if (eventEnvelope.Enrichment.Count > 0)
        {
            context["enrichment"] = eventEnvelope.Enrichment;
        }

        // Add asset context if available
        if (assetContext != null && assetContext.Count > 0)
        {
            context["asset_context"] = assetContext;
        }

        return context;
    }

    private async Task<(SeverityLevel adjustedSeverity, Dictionary<string, object>? assetContext)> DetermineSeverityWithAssetContext(
        EventEnvelope eventEnvelope, 
        SeverityLevel originalSeverity)
    {
        var assetContext = new Dictionary<string, object>();
        var shouldBoost = false;
        var highestCriticality = AssetCriticality.Low;

        // Check source asset
        if (eventEnvelope.Enrichment.TryGetValue("source_asset", out var sourceAssetObj) && 
            sourceAssetObj is JsonElement sourceAssetElement)
        {
            var sourceCriticality = ExtractAssetCriticality(sourceAssetElement);
            if (sourceCriticality.HasValue)
            {
                assetContext["source_asset"] = sourceAssetElement;
                if (sourceCriticality.Value >= AssetCriticality.High)
                {
                    shouldBoost = true;
                    highestCriticality = sourceCriticality.Value > highestCriticality ? sourceCriticality.Value : highestCriticality;
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(eventEnvelope.Normalized?.SourceIp))
        {
            // Fallback to direct asset lookup
            var sourceAsset = _assetCacheService.GetAsset(eventEnvelope.Normalized.SourceIp);
            if (sourceAsset != null)
            {
                assetContext["source_asset"] = new
                {
                    id = sourceAsset.Id.ToString(),
                    name = sourceAsset.Name,
                    criticality = sourceAsset.Criticality.ToString().ToLowerInvariant(),
                    owner = sourceAsset.Owner,
                    asset_type = sourceAsset.AssetType.ToString().ToLowerInvariant(),
                    tags = sourceAsset.Tags
                };

                if (sourceAsset.Criticality >= AssetCriticality.High)
                {
                    shouldBoost = true;
                    highestCriticality = sourceAsset.Criticality > highestCriticality ? sourceAsset.Criticality : highestCriticality;
                }
            }
        }

        // Check destination asset
        if (eventEnvelope.Enrichment.TryGetValue("destination_asset", out var destAssetObj) && 
            destAssetObj is JsonElement destAssetElement)
        {
            var destCriticality = ExtractAssetCriticality(destAssetElement);
            if (destCriticality.HasValue)
            {
                assetContext["destination_asset"] = destAssetElement;
                if (destCriticality.Value >= AssetCriticality.High)
                {
                    shouldBoost = true;
                    highestCriticality = destCriticality.Value > highestCriticality ? destCriticality.Value : highestCriticality;
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(eventEnvelope.Normalized?.DestinationIp))
        {
            // Fallback to direct asset lookup
            var destAsset = _assetCacheService.GetAsset(eventEnvelope.Normalized.DestinationIp);
            if (destAsset != null)
            {
                assetContext["destination_asset"] = new
                {
                    id = destAsset.Id.ToString(),
                    name = destAsset.Name,
                    criticality = destAsset.Criticality.ToString().ToLowerInvariant(),
                    owner = destAsset.Owner,
                    asset_type = destAsset.AssetType.ToString().ToLowerInvariant(),
                    tags = destAsset.Tags
                };

                if (destAsset.Criticality >= AssetCriticality.High)
                {
                    shouldBoost = true;
                    highestCriticality = destAsset.Criticality > highestCriticality ? destAsset.Criticality : highestCriticality;
                }
            }
        }

        // Boost severity if critical assets are involved
        var adjustedSeverity = shouldBoost ? BoostSeverity(originalSeverity) : originalSeverity;

        if (shouldBoost)
        {
            _logger.LogDebug("Alert severity boosted from {Original} to {Boosted} due to critical asset (criticality: {Criticality})", 
                originalSeverity, adjustedSeverity, highestCriticality);
        }

        return (adjustedSeverity, assetContext.Count > 0 ? assetContext : null);
    }

    private static AssetCriticality? ExtractAssetCriticality(JsonElement assetElement)
    {
        if (assetElement.TryGetProperty("criticality", out var criticalityElement))
        {
            var criticalityStr = criticalityElement.GetString();
            if (!string.IsNullOrWhiteSpace(criticalityStr) && 
                Enum.TryParse<AssetCriticality>(criticalityStr, true, out var criticality))
            {
                return criticality;
            }
        }
        return null;
    }

    private static SeverityLevel BoostSeverity(SeverityLevel originalSeverity)
    {
        return originalSeverity switch
        {
            SeverityLevel.Low => SeverityLevel.Medium,
            SeverityLevel.Medium => SeverityLevel.High,
            SeverityLevel.High => SeverityLevel.Critical,
            SeverityLevel.Critical => SeverityLevel.Critical, // Already at max
            _ => originalSeverity
        };
    }

    public async Task CreateAlertWithPlaybookActionsAsync(CorrelationRuleV2 rule, EventEnvelope eventEnvelope, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating alert with playbook actions for rule {RuleId} - {RuleName}", rule.Id, rule.Name);

            // Convert V2 rule to V1 for compatibility with existing logic
            var v1Rule = ConvertToV1Rule(rule);
            
            // Create the alert using existing logic
            await CreateAlertWithRiskScoringAsync(v1Rule, eventEnvelope, cancellationToken);

            // Check if rule has playbook actions and publish them
            if (rule.Actions?.Any(a => a.Type == ActionType.Playbook) == true && _alertActionPublisher != null)
            {
                // We need to get the created alert - for now, we'll query it back
                // In a real implementation, we'd modify CreateAlertWithRiskScoringAsync to return the alert
                var recentAlerts = await _alertRepository.GetAlertsByRuleIdAsync(rule.Id, DateTime.UtcNow.AddMinutes(-1), cancellationToken);
                var latestAlert = recentAlerts.OrderByDescending(a => a.CreatedAt).FirstOrDefault();
                
                if (latestAlert != null)
                {
                    var alertEntity = ConvertAlertRecordToEntity(latestAlert, eventEnvelope);
                    await _alertActionPublisher.PublishAsync(alertEntity, rule, cancellationToken);
                    
                    _logger.LogInformation("Published playbook action for alert {AlertId} from rule {RuleId}", latestAlert.Id, rule.Id);
                }
                else
                {
                    _logger.LogWarning("Could not find created alert for rule {RuleId} to publish playbook actions", rule.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create alert with playbook actions for rule {RuleId}", rule.Id);
            throw;
        }
    }

    private static CorrelationRule ConvertToV1Rule(CorrelationRuleV2 v2Rule)
    {
        return new CorrelationRule
        {
            Id = v2Rule.Id,
            Name = v2Rule.Name,
            Description = v2Rule.Description,
            Severity = v2Rule.Severity,
            Enabled = v2Rule.Enabled,
            Triggers = v2Rule.Triggers?.Select(t => new Trigger
            {
                Type = t.Type.ToString(),
                EventType = t.EventType,
                Source = t.Source,
                Filters = t.Filters
            }).ToList() ?? new(),
            Conditions = v2Rule.Conditions?.Select(c => new Condition
            {
                Field = c.Field,
                Operator = c.Operator.ToString(),
                Value = c.Value,
                CaseSensitive = c.CaseSensitive,
                Negate = c.Negate
            }).ToList() ?? new(),
            Actions = v2Rule.Actions?.Select(a => new Models.Action
            {
                Type = a.Type,
                Parameters = a.Parameters,
                Delay = a.Delay,
                Retry = a.Retry != null ? new Models.RetryPolicy
                {
                    Attempts = a.Retry.Attempts,
                    Delay = a.Retry.Delay,
                    Backoff = a.Retry.Backoff.ToString()
                } : null
            }).ToList() ?? new()
        };
    }

    private static AlertEntity ConvertAlertRecordToEntity(AlertRecord alertRecord, EventEnvelope eventEnvelope)
    {
        return new AlertEntity
        {
            Id = alertRecord.Id,
            RuleId = alertRecord.RuleId,
            RuleName = alertRecord.RuleName,
            Severity = alertRecord.Severity.ToString().ToLowerInvariant(),
            Status = alertRecord.Status.ToString().ToLowerInvariant(),
            TriggeredAt = alertRecord.TriggeredAt,
            Source = alertRecord.Source,
            CorrelationContext = JsonSerializer.Serialize(alertRecord.Context),
            MatchedConditions = JsonSerializer.Serialize(alertRecord.MatchedConditions),
            AggregationCount = alertRecord.AggregationCount,
            AggregatedValue = alertRecord.AggregatedValue,
            CreatedAt = alertRecord.CreatedAt,
            UpdatedAt = alertRecord.UpdatedAt
        };
    }
}