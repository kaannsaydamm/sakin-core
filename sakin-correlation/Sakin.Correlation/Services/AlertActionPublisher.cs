using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Configuration;
using Sakin.Common.Models.SOAR;
using Sakin.Correlation.Models;
using Sakin.Messaging.Producer;
using System.Text.Json;

namespace Sakin.Correlation.Services;

public interface IAlertActionPublisher
{
    Task PublishAsync(AlertEntity alert, CorrelationRuleV2 rule, CancellationToken cancellationToken = default);
}

public class AlertActionPublisher : IAlertActionPublisher
{
    private readonly IKafkaProducer _kafkaProducer;
    private readonly SoarKafkaTopics _kafkaTopics;
    private readonly ILogger<AlertActionPublisher> _logger;

    public AlertActionPublisher(
        IKafkaProducer kafkaProducer,
        IOptions<SoarKafkaTopics> kafkaTopicsOptions,
        ILogger<AlertActionPublisher> logger)
    {
        _kafkaProducer = kafkaProducer;
        _kafkaTopics = kafkaTopicsOptions.Value;
        _logger = logger;
    }

    public async Task PublishAsync(AlertEntity alert, CorrelationRuleV2 rule, CancellationToken cancellationToken = default)
    {
        try
        {
            var alertActionMessage = new AlertActionMessage(alert, rule);
            var messageJson = JsonSerializer.Serialize(alertActionMessage);

            await _kafkaProducer.ProduceAsync(
                _kafkaTopics.AlertActions,
                alert.Id.ToString(),
                messageJson,
                cancellationToken);

            _logger.LogInformation(
                "Published alert action for alert {AlertId} from rule {RuleId} to topic {Topic}",
                alert.Id,
                rule.Id,
                _kafkaTopics.AlertActions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish alert action for alert {AlertId} from rule {RuleId}",
                alert.Id,
                rule.Id);
            throw;
        }
    }
}