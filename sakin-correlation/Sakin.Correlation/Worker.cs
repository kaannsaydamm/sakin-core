using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Models;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Engine;
using Sakin.Correlation.Models;
using Sakin.Correlation.Services;
using Sakin.Messaging.Consumer;

namespace Sakin.Correlation;

public class Worker : BackgroundService
{
    private readonly IKafkaConsumer _consumer;
    private readonly ILogger<Worker> _logger;
    private readonly KafkaWorkerOptions _options;
    private readonly IRuleLoaderService _ruleLoader;
    private readonly IRuleLoaderServiceV2 _ruleLoaderV2;
    private readonly IRuleEvaluatorV2 _ruleEvaluator;
    private readonly IAlertCreatorService _alertCreator;
    private readonly IMetricsService _metricsService;

    public Worker(
        IKafkaConsumer consumer,
        IOptions<KafkaWorkerOptions> options,
        ILogger<Worker> logger,
        IRuleLoaderService ruleLoader,
        IRuleLoaderServiceV2 ruleLoaderV2,
        IRuleEvaluatorV2 ruleEvaluator,
        IAlertCreatorService alertCreator,
        IMetricsService metricsService)
    {
        _consumer = consumer;
        _logger = logger;
        _options = options.Value;
        _ruleLoader = ruleLoader;
        _ruleLoaderV2 = ruleLoaderV2;
        _ruleEvaluator = ruleEvaluator;
        _alertCreator = alertCreator;
        _metricsService = metricsService;
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
        var startTime = DateTimeOffset.UtcNow;
        
        try
        {
            _metricsService.IncrementEventsProcessed();
            
            var legacyRules = _ruleLoader.Rules;
            var v2Rules = _ruleLoaderV2.RulesV2;
            _logger.LogDebug("Evaluating event {EventId} against {LegacyRuleCount} legacy rules and {V2RuleCount} V2 rules", 
                eventEnvelope.EventId, legacyRules.Count, v2Rules.Count);

            // Evaluate legacy rules
            foreach (var rule in legacyRules)
            {
                try
                {
                    _metricsService.IncrementRulesEvaluated();
                    var evaluationResult = await _ruleEvaluator.EvaluateAsync(rule, eventEnvelope);
                    
                    if (evaluationResult.IsMatch && evaluationResult.ShouldTriggerAlert)
                    {
                        _logger.LogInformation(
                            "Legacy rule {RuleId} ({RuleName}) matched for event {EventId}, creating alert",
                            rule.Id, rule.Name, eventEnvelope.EventId);

                        await _alertCreator.CreateAlertAsync(rule, eventEnvelope, cancellationToken);
                    }
                    else if (evaluationResult.IsMatch)
                    {
                        _logger.LogDebug(
                            "Legacy rule {RuleId} ({RuleName}) matched but should not trigger alert (aggregation rule)",
                            rule.Id, rule.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error evaluating legacy rule {RuleId} for event {EventId}", 
                        rule.Id, eventEnvelope.EventId);
                }
            }

            // Evaluate V2 rules
            foreach (var rule in v2Rules)
            {
                try
                {
                    _metricsService.IncrementRulesEvaluated();
                    var evaluationResult = await _ruleEvaluator.EvaluateAsync(rule, eventEnvelope);
                    
                    if (evaluationResult.IsMatch && evaluationResult.ShouldTriggerAlert)
                    {
                        _logger.LogInformation(
                            "V2 rule {RuleId} ({RuleName}) matched for event {EventId}, creating alert",
                            rule.Id, rule.Name, eventEnvelope.EventId);

                        // Convert V2 rule to legacy format for alert creation
                        var legacyRule = ConvertToLegacyRule(rule);
                        await _alertCreator.CreateAlertAsync(legacyRule, eventEnvelope, cancellationToken);
                    }
                    else if (evaluationResult.IsMatch)
                    {
                        _logger.LogDebug(
                            "V2 rule {RuleId} ({RuleName}) matched but should not trigger alert (aggregation threshold not reached)",
                            rule.Id, rule.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error evaluating V2 rule {RuleId} for event {EventId}", 
                        rule.Id, eventEnvelope.EventId);
                }
            }
            
            var elapsedMs = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            _metricsService.RecordProcessingLatency(elapsedMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event {EventId}", eventEnvelope.EventId);
            var elapsedMs = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            _metricsService.RecordProcessingLatency(elapsedMs);
        }
    }

    private CorrelationRule ConvertToLegacyRule(CorrelationRuleV2 v2Rule)
    {
        return new CorrelationRule
        {
            Id = v2Rule.Id,
            Name = v2Rule.Name,
            Description = v2Rule.Description,
            Enabled = v2Rule.Enabled,
            Severity = Enum.TryParse<SeverityLevel>(v2Rule.Severity, true, out var severity) ? severity : SeverityLevel.Medium,
            Triggers = new List<Trigger>
            {
                new()
                {
                    Type = TriggerType.Event,
                    EventType = v2Rule.Trigger.SourceTypes?.FirstOrDefault() ?? "all",
                    Filters = v2Rule.Trigger.Match?.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value
                    ) ?? new Dictionary<string, object>()
                }
            },
            Conditions = string.IsNullOrEmpty(v2Rule.Condition.Field) 
                ? new List<Condition>()
                : new List<Condition>
                {
                    new()
                    {
                        Field = v2Rule.Condition.Field,
                        Operator = ParseConditionOperator(v2Rule.Condition.Operator),
                        Value = v2Rule.Condition.Value
                    }
                },
            Actions = v2Rule.Actions?.Select(action => new Sakin.Correlation.Models.Action
            {
                Type = ActionType.Alert,
                Parameters = new Dictionary<string, object>()
            }).ToList() ?? new List<Sakin.Correlation.Models.Action>(),
            Metadata = v2Rule.Metadata
        };
    }

    private ConditionOperator ParseConditionOperator(string operatorStr)
    {
        return operatorStr.ToLowerInvariant() switch
        {
            "equals" => ConditionOperator.Equals,
            "not_equals" => ConditionOperator.NotEquals,
            "contains" => ConditionOperator.Contains,
            "not_contains" => ConditionOperator.NotContains,
            "starts_with" => ConditionOperator.StartsWith,
            "ends_with" => ConditionOperator.EndsWith,
            "greater_than" => ConditionOperator.GreaterThan,
            "greater_than_or_equal" => ConditionOperator.GreaterThanOrEqual,
            "less_than" => ConditionOperator.LessThan,
            "less_than_or_equal" => ConditionOperator.LessThanOrEqual,
            "in" => ConditionOperator.In,
            "not_in" => ConditionOperator.NotIn,
            "regex" => ConditionOperator.Regex,
            "exists" => ConditionOperator.Exists,
            "not_exists" => ConditionOperator.NotExists,
            "gte" => ConditionOperator.GreaterThanOrEqual,
            "lte" => ConditionOperator.LessThanOrEqual,
            "gt" => ConditionOperator.GreaterThan,
            "lt" => ConditionOperator.LessThan,
            _ => ConditionOperator.Equals
        };
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
