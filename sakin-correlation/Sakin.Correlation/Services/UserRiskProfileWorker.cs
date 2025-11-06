using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Cache;
using Sakin.Common.Models;
using Sakin.Correlation.Configuration;

namespace Sakin.Correlation.Services;

public class UserRiskProfileWorker : BackgroundService
{
    private readonly IUserRiskProfileService _userRiskProfileService;
    private readonly ILogger<UserRiskProfileWorker> _logger;
    private readonly KafkaConfiguration _kafkaConfig;
    private readonly IRedisClient _redisClient;

    public UserRiskProfileWorker(
        IUserRiskProfileService userRiskProfileService,
        ILogger<UserRiskProfileWorker> logger,
        IOptions<KafkaConfiguration> kafkaConfig,
        IRedisClient redisClient)
    {
        _userRiskProfileService = userRiskProfileService;
        _logger = logger;
        _kafkaConfig = kafkaConfig.Value;
        _redisClient = redisClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UserRiskProfileWorker started - listening for normalized events");

        // For now, we'll implement a simplified version that processes events from Redis
        // In a full implementation, this would use a Kafka consumer
        await ProcessEventsFromRedisAsync(stoppingToken);
    }

    private async Task ProcessEventsFromRedisAsync(CancellationToken cancellationToken)
    {
        var processedEventsKey = "sakin:user_risk:processed_events";
        var eventQueueKey = "sakin:user_risk:event_queue";

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Try to get an event from the queue
                var eventData = await _redisClient.ListLeftPopAsync<string>(eventQueueKey);
                
                if (eventData != null)
                {
                    try
                    {
                        var normalizedEvent = System.Text.Json.JsonSerializer.Deserialize<NormalizedEvent>(eventData);
                        if (normalizedEvent != null)
                        {
                            await _userRiskProfileService.UpdateUserRiskProfileAsync(normalizedEvent);
                            
                            // Mark event as processed
                            await _redisClient.SetAddAsync(processedEventsKey, normalizedEvent.Id, TimeSpan.FromDays(1));
                            
                            _logger.LogDebug("Processed user risk profile update for event {EventId}, user {Username}", 
                                normalizedEvent.Id, normalizedEvent.Username);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process normalized event for user risk profiling");
                    }
                }
                else
                {
                    // No events in queue, wait a bit
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UserRiskProfileWorker main loop");
                await Task.Delay(5000, cancellationToken); // Wait before retrying
            }
        }

        _logger.LogInformation("UserRiskProfileWorker stopped");
    }

    // Helper method to queue events for processing (called by other services)
    public async Task QueueEventAsync(NormalizedEvent normalizedEvent)
    {
        try
        {
            var eventData = System.Text.Json.JsonSerializer.Serialize(normalizedEvent);
            var eventQueueKey = "sakin:user_risk:event_queue";
            
            await _redisClient.ListRightPushAsync(eventQueueKey, eventData);
            
            _logger.LogDebug("Queued event {EventId} for user risk profiling", normalizedEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue event {EventId} for user risk profiling", normalizedEvent.Id);
        }
    }
}