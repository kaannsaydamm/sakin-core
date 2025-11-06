using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Sakin.Common.Models;
using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Entities;
using Sakin.Correlation.Persistence.Repositories;

namespace Sakin.Correlation.Services;

public class RiskScoringWorker : BackgroundService
{
    private readonly Channel<RiskScoringRequest> _scoringChannel;
    private readonly IRiskScoringService _riskScoringService;
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<RiskScoringWorker> _logger;
    private readonly ChannelWriter<RiskScoringRequest> _channelWriter;
    private readonly ChannelReader<RiskScoringRequest> _channelReader;

    public RiskScoringWorker(
        IRiskScoringService riskScoringService,
        IAlertRepository alertRepository,
        ILogger<RiskScoringWorker> logger)
    {
        _riskScoringService = riskScoringService;
        _alertRepository = alertRepository;
        _logger = logger;
        
        // Create an unbounded channel for risk scoring requests
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _scoringChannel = Channel.CreateBounded<RiskScoringRequest>(options);
        _channelWriter = _scoringChannel.Writer;
        _channelReader = _scoringChannel.Reader;
    }

    public ChannelWriter<RiskScoringRequest> ChannelWriter => _channelWriter;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RiskScoringWorker started");

        while (!stoppingToken.IsCancellationRequested && await _channelReader.WaitToReadAsync(stoppingToken))
        {
            try
            {
                while (_channelReader.TryRead(out var request))
                {
                    await ProcessRiskScoringRequestAsync(request, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing risk scoring requests");
            }
        }

        _logger.LogInformation("RiskScoringWorker stopped");
    }

    private async Task ProcessRiskScoringRequestAsync(RiskScoringRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Processing risk scoring for alert {AlertId}", request.AlertId);

            // Calculate risk score
            var riskScore = await _riskScoringService.CalculateRiskAsync(request.Alert, request.EventEnvelope);

            // Update the alert with risk scoring information
            await UpdateAlertWithRiskScoreAsync(request.AlertId, riskScore, cancellationToken);

            _logger.LogInformation("Risk scoring completed for alert {AlertId}: Score={Score}, Level={Level}", 
                request.AlertId, riskScore.Score, riskScore.Level);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process risk scoring for alert {AlertId}", request.AlertId);
        }
    }

    private async Task UpdateAlertWithRiskScoreAsync(Guid alertId, RiskScore riskScore, CancellationToken cancellationToken)
    {
        try
        {
            // Get the existing alert
            var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken);
            if (alert == null)
            {
                _logger.LogWarning("Alert {AlertId} not found for risk scoring update", alertId);
                return;
            }

            // Update risk scoring fields
            alert.RiskScore = riskScore.Score;
            alert.RiskLevel = riskScore.Level.ToString().ToLowerInvariant();
            alert.RiskFactors = JsonSerializer.Serialize(riskScore.Factors);
            alert.Reasoning = riskScore.Reasoning;
            alert.Status = "scored"; // Update status to indicate scoring is complete
            alert.UpdatedAt = DateTimeOffset.UtcNow;

            // Save the updated alert
            await _alertRepository.UpdateAsync(alert, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update alert {AlertId} with risk score", alertId);
            throw;
        }
    }

    public async Task QueueRiskScoringAsync(Guid alertId, AlertEntity alert, EventEnvelope eventEnvelope)
    {
        try
        {
            var request = new RiskScoringRequest
            {
                AlertId = alertId,
                Alert = alert,
                EventEnvelope = eventEnvelope
            };

            await _channelWriter.WriteAsync(request);
            _logger.LogDebug("Queued risk scoring for alert {AlertId}", alertId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue risk scoring for alert {AlertId}", alertId);
        }
    }
}

public record RiskScoringRequest
{
    public Guid AlertId { get; init; }
    public AlertEntity Alert { get; init; } = null!;
    public EventEnvelope EventEnvelope { get; init; } = null!;
}