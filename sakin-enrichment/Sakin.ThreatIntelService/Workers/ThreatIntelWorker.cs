using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sakin.Common.Models;
using Sakin.Messaging.Consumer;
using Sakin.ThreatIntelService.Services;

namespace Sakin.ThreatIntelService.Workers
{
    public class ThreatIntelWorker : BackgroundService
    {
        private readonly IKafkaConsumer _consumer;
        private readonly IThreatIntelService _threatIntelService;
        private readonly ILogger<ThreatIntelWorker> _logger;

        public ThreatIntelWorker(
            IKafkaConsumer consumer,
            IThreatIntelService threatIntelService,
            ILogger<ThreatIntelWorker> logger)
        {
            _consumer = consumer;
            _threatIntelService = threatIntelService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting threat intel worker");

            try
            {
                await _consumer.ConsumeAsync<ThreatIntelLookupRequest>(HandleMessageAsync, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Threat intel worker cancellation requested");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in threat intel worker");
                throw;
            }
        }

        private async Task HandleMessageAsync(ConsumeResult<ThreatIntelLookupRequest> result)
        {
            if (result.Message is null)
            {
                _logger.LogWarning("Received null message on topic {Topic}", result.Topic);
                return;
            }

            try
            {
                _logger.LogDebug(
                    "Processing threat intel lookup request: Type={Type}, Value={Value} (topic: {Topic}, partition: {Partition}, offset: {Offset})",
                    result.Message.Type,
                    result.Message.Value,
                    result.Topic,
                    result.Partition,
                    result.Offset);

                var score = await _threatIntelService.ProcessAsync(result.Message);

                _logger.LogInformation(
                    "Threat intel lookup completed: Type={Type}, Value={Value}, Score={Score}, Malicious={Malicious}",
                    result.Message.Type,
                    result.Message.Value,
                    score.Score,
                    score.IsKnownMalicious);

                _consumer.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing threat intel lookup for {Type} {Value}", result.Message.Type, result.Message.Value);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping threat intel worker");
            await base.StopAsync(cancellationToken);
        }
    }
}
