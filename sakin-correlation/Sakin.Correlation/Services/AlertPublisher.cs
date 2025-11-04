using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Models;
using Sakin.Messaging.Producer;

namespace Sakin.Correlation.Services;

public class AlertPublisher : IAlertPublisher
{
    private readonly IKafkaProducer _producer;
    private readonly CorrelationKafkaOptions _options;
    private readonly ILogger<AlertPublisher> _logger;

    public AlertPublisher(IKafkaProducer producer, IOptions<CorrelationKafkaOptions> options, ILogger<AlertPublisher> logger)
    {
        _producer = producer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(Alert alert, CancellationToken cancellationToken)
    {
        try
        {
            var topic = string.IsNullOrWhiteSpace(_options.AlertsTopic) ? "alerts" : _options.AlertsTopic;
            var key = $"{alert.RuleId}:{alert.SourceIp}";

            await _producer.ProduceAsync(topic, alert, key, cancellationToken);

            _logger.LogInformation(
                "Published alert {AlertId} for rule {RuleName} (severity: {Severity}) to topic {Topic}",
                alert.Id, alert.RuleName, alert.Severity, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish alert {AlertId} for rule {RuleName}", alert.Id, alert.RuleName);
            throw;
        }
    }
}
