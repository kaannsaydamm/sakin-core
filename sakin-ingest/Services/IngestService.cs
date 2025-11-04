using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Ingest.Configuration;
using Sakin.Ingest.Pipelines;
using Sakin.Ingest.Sinks;
using Sakin.Ingest.Sources;

namespace Sakin.Ingest.Services
{
    public class IngestService : BackgroundService, IHealthCheck
    {
        private readonly IEventSource _eventSource;
        private readonly IEventPipeline _pipeline;
        private readonly IEventSink _eventSink;
        private readonly IngestOptions _options;
        private readonly ILogger<IngestService> _logger;
        private readonly HealthCheckResult _healthStatus = HealthCheckResult.Healthy();

        public IngestService(
            IEventSource eventSource,
            IEventPipeline pipeline,
            IEventSink eventSink,
            IOptions<IngestOptions> options,
            ILogger<IngestService> logger)
        {
            _eventSource = eventSource;
            _pipeline = pipeline;
            _eventSink = eventSink;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting Sakin Ingest Service");

            try
            {
                // Start the event source
                await _eventSource.StartAsync(stoppingToken);

                // Subscribe to raw events
                _eventSource.OnRawEventReceived += async (sender, rawEvent) =>
                {
                    await ProcessRawEventAsync(rawEvent, stoppingToken);
                };

                _logger.LogInformation("Sakin Ingest Service started successfully");

                // Keep the service running until cancellation is requested
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in ingest service");
                throw;
            }
        }

        private async Task ProcessRawEventAsync(RawEvent rawEvent, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Processing raw event {EventId} from source {Source}", rawEvent.Id, rawEvent.Source);

                // Process through pipeline
                var normalizedEvent = await _pipeline.ProcessAsync(rawEvent, cancellationToken);
                
                if (normalizedEvent != null)
                {
                    // Publish to sink
                    await _eventSink.PublishAsync(normalizedEvent, cancellationToken);
                    
                    _logger.LogDebug("Successfully processed and published event {EventId}", normalizedEvent.Id);
                }
                else
                {
                    _logger.LogWarning("Pipeline returned null for event {EventId}", rawEvent.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing raw event {EventId}", rawEvent.Id);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Sakin Ingest Service");
            
            try
            {
                await _eventSource.StopAsync(cancellationToken);
                _logger.LogInformation("Sakin Ingest Service stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping ingest service");
            }
            finally
            {
                await base.StopAsync(cancellationToken);
            }
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_healthStatus);
        }
    }
}