using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Models;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Engine;
using Sakin.Correlation.Services;
using Sakin.Messaging.Consumer;

namespace Sakin.Correlation;

public class Worker : BackgroundService
{
    private readonly IKafkaConsumer _consumer;
    private readonly ILogger<Worker> _logger;
    private readonly KafkaWorkerOptions _options;
    private readonly IRuleLoaderService _ruleLoader;
    private readonly IRuleEvaluator _ruleEvaluator;
    private readonly IAlertCreatorService _alertCreator;

    public Worker(
        IKafkaConsumer consumer,
        IOptions<KafkaWorkerOptions> options,
        ILogger<Worker> logger,
        IRuleLoaderService ruleLoader,
        IRuleEvaluator ruleEvaluator,
        IAlertCreatorService alertCreator)
    {
        _consumer = consumer;
        _logger = logger;
        _options = options.Value;
        _ruleLoader = ruleLoader;
        _ruleEvaluator = ruleEvaluator;
        _alertCreator = alertCreator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Topic))
        {
            _logger.LogWarning("Kafka topic is not configured. The correlation worker will not consume messages.");
            return;
        }

        _logger.LogInformation(
            "Correlation worker started. Listening topic {Topic} with consumer group {ConsumerGroup}.",
            _options.Topic,
            _options.ConsumerGroup);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _consumer.ConsumeAsync<EventEnvelope>(result =>
                {
                    if (result.Message is null)
                    {
                        _logger.LogWarning(
                            "Received empty message from topic {Topic} at offset {Offset}.",
                            result.Topic,
                            result.Offset);
                        return Task.CompletedTask;
                    }

                    _logger.LogInformation(
                        "Consumed event envelope {@EventEnvelope} from topic {Topic}, partition {Partition}, offset {Offset}.",
                        result.Message,
                        result.Topic,
                        result.Partition,
                        result.Offset);

                    return ProcessEventAsync(result.Message, stoppingToken);
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Correlation worker cancellation requested.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while consuming Kafka messages. Retrying...");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ProcessEventAsync(EventEnvelope eventEnvelope, CancellationToken cancellationToken)
    {
        try
        {
            var rules = _ruleLoader.Rules;
            _logger.LogDebug("Evaluating event {EventId} against {RuleCount} rules", 
                eventEnvelope.EventId, rules.Count);

            foreach (var rule in rules)
            {
                try
                {
                    var evaluationResult = await _ruleEvaluator.EvaluateAsync(rule, eventEnvelope);
                    
                    if (evaluationResult.IsMatch && evaluationResult.ShouldTriggerAlert)
                    {
                        _logger.LogInformation(
                            "Rule {RuleId} ({RuleName}) matched for event {EventId}, creating alert",
                            rule.Id, rule.Name, eventEnvelope.EventId);

                        await _alertCreator.CreateAlertAsync(rule, eventEnvelope, cancellationToken);
                    }
                    else if (evaluationResult.IsMatch)
                    {
                        _logger.LogDebug(
                            "Rule {RuleId} ({RuleName}) matched but should not trigger alert (aggregation rule)",
                            rule.Id, rule.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error evaluating rule {RuleId} for event {EventId}", 
                        rule.Id, eventEnvelope.EventId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event {EventId}", eventEnvelope.EventId);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping correlation worker.");
        _consumer.Unsubscribe();
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
