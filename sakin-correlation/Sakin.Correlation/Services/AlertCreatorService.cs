using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sakin.Common.Models;
using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Models;
using Sakin.Correlation.Persistence.Repositories;

namespace Sakin.Correlation.Services;

public class AlertCreatorService : IAlertCreatorService
{
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<AlertCreatorService> _logger;
    private readonly IMetricsService? _metricsService;

    public AlertCreatorService(
        IAlertRepository alertRepository,
        ILogger<AlertCreatorService> logger,
        IMetricsService? metricsService = null)
    {
        _alertRepository = alertRepository;
        _logger = logger;
        _metricsService = metricsService;
    }

    public async Task CreateAlertAsync(CorrelationRule rule, EventEnvelope eventEnvelope, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating alert for rule {RuleId} - {RuleName}", rule.Id, rule.Name);

            var alertRecord = new AlertRecord
            {
                Id = Guid.NewGuid(),
                RuleId = rule.Id,
                RuleName = rule.Name,
                Severity = rule.Severity,
                Status = AlertStatus.New,
                TriggeredAt = DateTimeOffset.UtcNow,
                Source = eventEnvelope.Source,
                Context = BuildContext(eventEnvelope),
                MatchedConditions = Array.Empty<string>(), // Will be populated by rule evaluator
                AggregationCount = null, // Stateless rules don't have aggregation
                AggregatedValue = null, // Stateless rules don't have aggregation
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var createdAlert = await _alertRepository.CreateAsync(alertRecord, cancellationToken);
            
            _metricsService?.IncrementAlertsCreated();
            
            _logger.LogInformation(
                "Alert created successfully: {AlertId} for rule {RuleId} with severity {Severity}", 
                createdAlert.Id, rule.Id, rule.Severity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create alert for rule {RuleId}", rule.Id);
            throw;
        }
    }

    private static Dictionary<string, object?> BuildContext(EventEnvelope eventEnvelope)
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

        return context;
    }
}