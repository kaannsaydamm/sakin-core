using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sakin.Correlation.Models;
using Sakin.Common.Models;

namespace Sakin.Correlation.Engine;

public class RuleEvaluator : IRuleEvaluator
{
    private readonly ILogger<RuleEvaluator> _logger;

    private sealed record ConditionEvaluation(bool IsMatch, List<string> MatchedConditions, string? FailureReason)
    {
        public static ConditionEvaluation Success(List<string> matched) => new(true, matched, null);
        public static ConditionEvaluation Failure(string reason, List<string>? matched = null) => new(false, matched ?? new List<string>(), reason);
    }

    private sealed record MatchedEvent(EventEnvelope Envelope, List<string> MatchedConditions);

    private sealed record AggregateMetrics(string FieldName, double Value, int EventCount);

    public RuleEvaluator(ILogger<RuleEvaluator> logger)
    {
        _logger = logger;
    }

    public Task<EvaluationResult> EvaluateAsync(CorrelationRule rule, EventEnvelope eventEnvelope)
    {
        if (!rule.Enabled)
        {
            return Task.FromResult(new EvaluationResult
            {
                IsMatch = false,
                ShouldTriggerAlert = false,
                Reason = "Rule is disabled"
            });
        }

        if (eventEnvelope.Normalized is null)
        {
            return Task.FromResult(new EvaluationResult
            {
                IsMatch = false,
                ShouldTriggerAlert = false,
                Reason = "Event has no normalized data"
            });
        }

        var evaluation = EvaluateEvent(rule, eventEnvelope);
        if (!evaluation.IsMatch)
        {
            return Task.FromResult(new EvaluationResult
            {
                IsMatch = false,
                ShouldTriggerAlert = false,
                Reason = evaluation.FailureReason,
                MatchedConditions = evaluation.MatchedConditions
            });
        }

        return Task.FromResult(new EvaluationResult
        {
            IsMatch = true,
            ShouldTriggerAlert = rule.Aggregation is null,
            Reason = rule.Aggregation is null
                ? "All conditions matched"
                : "Conditions matched within aggregation window",
            MatchedConditions = evaluation.MatchedConditions,
            AggregationCount = rule.Aggregation is null ? 1 : null,
            Context = BuildContext(eventEnvelope)
        });
    }

    public Task<EvaluationResult> EvaluateWithAggregationAsync(CorrelationRule rule, IEnumerable<EventEnvelope> events)
    {
        if (!rule.Enabled)
        {
            return Task.FromResult(new EvaluationResult
            {
                IsMatch = false,
                ShouldTriggerAlert = false,
                Reason = "Rule is disabled"
            });
        }

        var orderedEvents = events
            .Where(e => e.Normalized is not null)
            .OrderBy(e => e.Normalized!.Timestamp)
            .ToList();

        if (orderedEvents.Count == 0)
        {
            return Task.FromResult(new EvaluationResult
            {
                IsMatch = false,
                ShouldTriggerAlert = false,
                Reason = "No events to evaluate"
            });
        }

        if (rule.Aggregation is null)
        {
            return EvaluateAsync(rule, orderedEvents.Last());
        }

        var matchedEvents = new List<MatchedEvent>();
        foreach (var evt in orderedEvents)
        {
            var evaluation = EvaluateEvent(rule, evt);
            if (evaluation.IsMatch)
            {
                matchedEvents.Add(new MatchedEvent(evt, evaluation.MatchedConditions));
            }
        }

        if (matchedEvents.Count == 0)
        {
            return Task.FromResult(new EvaluationResult
            {
                IsMatch = false,
                ShouldTriggerAlert = false,
                Reason = "No events matched rule criteria",
                AggregationCount = 0
            });
        }

        var windowedEvents = ApplyAggregationWindow(rule.Aggregation, matchedEvents);
        if (windowedEvents.Count == 0)
        {
            return Task.FromResult(new EvaluationResult
            {
                IsMatch = false,
                ShouldTriggerAlert = false,
                Reason = "No events fell within aggregation window",
                AggregationCount = 0
            });
        }

        var aggregationResult = EvaluateAggregation(rule.Aggregation, windowedEvents);
        return Task.FromResult(aggregationResult);
    }

    private ConditionEvaluation EvaluateEvent(CorrelationRule rule, EventEnvelope eventEnvelope)
    {
        if (!MatchesTriggers(rule.Triggers, eventEnvelope))
        {
            return ConditionEvaluation.Failure("Event did not match trigger criteria");
        }

        var conditionEvaluation = EvaluateConditions(rule.Conditions, eventEnvelope);
        if (!conditionEvaluation.IsMatch)
        {
            return ConditionEvaluation.Failure(conditionEvaluation.FailureReason ?? "Conditions failed", conditionEvaluation.MatchedConditions);
        }

        return ConditionEvaluation.Success(conditionEvaluation.MatchedConditions);
    }

    private bool MatchesTriggers(List<Trigger> triggers, EventEnvelope eventEnvelope)
    {
        if (triggers is null || triggers.Count == 0)
        {
            return true;
        }

        var normalizedEvent = eventEnvelope.Normalized!;

        foreach (var trigger in triggers)
        {
            if (trigger.Type != TriggerType.Event)
            {
                continue;
            }

            if (!TriggerMatchesEventType(trigger, normalizedEvent))
            {
                continue;
            }

            if (trigger.Filters != null && trigger.Filters.Count > 0)
            {
                if (EvaluateFilters(trigger.Filters, eventEnvelope))
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    private bool TriggerMatchesEventType(Trigger trigger, NormalizedEvent normalizedEvent)
    {
        if (string.IsNullOrWhiteSpace(trigger.EventType))
        {
            return true;
        }

        var triggerType = NormalizeKey(trigger.EventType);
        var eventType = NormalizeKey(normalizedEvent.EventType.ToString());

        if (triggerType == eventType)
        {
            return true;
        }

        var mappedEventType = ConvertEventTypeString(triggerType);
        return mappedEventType != EventType.Unknown && mappedEventType == normalizedEvent.EventType;
    }

    private bool EvaluateFilters(Dictionary<string, object> filters, EventEnvelope eventEnvelope)
    {
        foreach (var filter in filters)
        {
            var eventValue = GetFieldValue(eventEnvelope, filter.Key);
            if (eventValue is null)
            {
                return false;
            }

            var filterValue = filter.Value;
            if (!CompareValues(eventValue, filterValue, caseSensitive: false))
            {
                return false;
            }
        }

        return true;
    }

    private ConditionEvaluation EvaluateConditions(List<Condition> conditions, EventEnvelope eventEnvelope)
    {
        if (conditions is null || conditions.Count == 0)
        {
            return ConditionEvaluation.Success(new List<string>());
        }

        var matchedConditions = new List<string>();

        foreach (var condition in conditions)
        {
            var fieldValue = GetFieldValue(eventEnvelope, condition.Field);
            var conditionMet = EvaluateCondition(condition, fieldValue);

            if (!conditionMet)
            {
                return ConditionEvaluation.Failure(
                    $"Condition failed: {condition.Field} {condition.Operator} {condition.Value}",
                    matchedConditions);
            }

            var conditionDesc = condition.Value != null
                ? $"{condition.Field} {condition.Operator} {condition.Value}"
                : $"{condition.Field} {condition.Operator}";
            matchedConditions.Add(conditionDesc);
        }

        return ConditionEvaluation.Success(matchedConditions);
    }

    private bool EvaluateCondition(Condition condition, object? fieldValue)
    {
        var result = condition.Operator switch
        {
            ConditionOperator.Exists => fieldValue is not null,
            ConditionOperator.NotExists => fieldValue is null,
            ConditionOperator.Equals => CompareValues(fieldValue, condition.Value, condition.CaseSensitive),
            ConditionOperator.NotEquals => !CompareValues(fieldValue, condition.Value, condition.CaseSensitive),
            ConditionOperator.Contains => ContainsValue(fieldValue, condition.Value, condition.CaseSensitive),
            ConditionOperator.NotContains => !ContainsValue(fieldValue, condition.Value, condition.CaseSensitive),
            ConditionOperator.StartsWith => StartsWithValue(fieldValue, condition.Value, condition.CaseSensitive),
            ConditionOperator.EndsWith => EndsWithValue(fieldValue, condition.Value, condition.CaseSensitive),
            ConditionOperator.GreaterThan => CompareNumeric(fieldValue, condition.Value, (a, b) => a > b),
            ConditionOperator.GreaterThanOrEqual => CompareNumeric(fieldValue, condition.Value, (a, b) => a >= b),
            ConditionOperator.LessThan => CompareNumeric(fieldValue, condition.Value, (a, b) => a < b),
            ConditionOperator.LessThanOrEqual => CompareNumeric(fieldValue, condition.Value, (a, b) => a <= b),
            ConditionOperator.In => IsInList(fieldValue, condition.Value, condition.CaseSensitive),
            ConditionOperator.NotIn => !IsInList(fieldValue, condition.Value, condition.CaseSensitive),
            ConditionOperator.Regex => MatchesRegex(fieldValue, condition.Value),
            _ => false
        };

        return condition.Negate ? !result : result;
    }

    private object? GetFieldValue(EventEnvelope eventEnvelope, string fieldPath)
    {
        if (eventEnvelope.Normalized is null || string.IsNullOrWhiteSpace(fieldPath))
        {
            return null;
        }

        var segments = fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        if (segments.Length == 1)
        {
            return GetSimpleFieldValue(eventEnvelope, segments[0]);
        }

        var prefix = NormalizeKey(segments[0]);
        var remainder = string.Join('.', segments.Skip(1));

        return prefix switch
        {
            "metadata" => GetMetadataValue(eventEnvelope.Normalized, remainder),
            "enrichment" => GetDictionaryValue(eventEnvelope.Enrichment, remainder),
            _ => GetSimpleFieldValue(eventEnvelope, fieldPath)
        };
    }

    private object? GetSimpleFieldValue(EventEnvelope eventEnvelope, string field)
    {
        var normalized = eventEnvelope.Normalized!;
        var key = NormalizeKey(field);

        return key switch
        {
            "username" => GetMetadataValue(normalized, "username"),
            "sourceip" => normalized.SourceIp,
            "source_ip" => normalized.SourceIp,
            "destinationip" => normalized.DestinationIp,
            "destination_ip" => normalized.DestinationIp,
            "sourceport" => normalized.SourcePort,
            "source_port" => normalized.SourcePort,
            "destinationport" => normalized.DestinationPort,
            "destination_port" => normalized.DestinationPort,
            "protocol" => normalized.Protocol.ToString(),
            "eventtype" => normalized.EventType.ToString(),
            "event_type" => normalized.EventType.ToString(),
            "severity" => normalized.Severity.ToString(),
            "payload" => normalized.Payload,
            "devicename" => normalized.DeviceName,
            "device_name" => normalized.DeviceName,
            "sensorid" => normalized.SensorId,
            "sensor_id" => normalized.SensorId,
            "timestamp" => normalized.Timestamp,
            "hourofday" => normalized.Timestamp.Hour,
            "hour_of_day" => normalized.Timestamp.Hour,
            "dayofweek" => normalized.Timestamp.DayOfWeek.ToString(),
            "day_of_week" => normalized.Timestamp.DayOfWeek.ToString(),
            "failurereason" => GetMetadataValue(normalized, "failure_reason"),
            "failure_reason" => GetMetadataValue(normalized, "failure_reason"),
            "processname" => GetMetadataValue(normalized, "process_name"),
            "process_name" => GetMetadataValue(normalized, "process_name"),
            "processpath" => GetMetadataValue(normalized, "process_path"),
            "process_path" => GetMetadataValue(normalized, "process_path"),
            "filepath" => GetMetadataValue(normalized, "file_path"),
            "file_path" => GetMetadataValue(normalized, "file_path"),
            "userrole" => GetMetadataValue(normalized, "user_role"),
            "user_role" => GetMetadataValue(normalized, "user_role"),
            "digitalsignature" => GetMetadataValue(normalized, "digital_signature"),
            "digital_signature" => GetMetadataValue(normalized, "digital_signature"),
            "queryname" => GetMetadataValue(normalized, "queryName") ?? GetMetadataValue(normalized, "query_name"),
            "query_name" => GetMetadataValue(normalized, "query_name") ?? GetMetadataValue(normalized, "queryName"),
            "bytesent" => GetMetadataValue(normalized, "bytes_sent"),
            "bytes_sent" => GetMetadataValue(normalized, "bytes_sent"),
            "bytesreceived" => GetMetadataValue(normalized, "bytes_received"),
            "bytes_received" => GetMetadataValue(normalized, "bytes_received"),
            "source" => eventEnvelope.Source,
            _ => GetMetadataValue(normalized, field)
        };
    }

    private object? GetMetadataValue(NormalizedEvent normalizedEvent, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (normalizedEvent.Metadata.TryGetValue(key, out var value))
        {
            return value;
        }

        var normalizedKey = NormalizeKey(key);
        var candidates = GenerateKeyVariants(normalizedKey).ToList();

        foreach (var candidate in candidates)
        {
            if (normalizedEvent.Metadata.TryGetValue(candidate, out value))
            {
                return value;
            }
        }

        foreach (var candidate in candidates)
        {
            var match = normalizedEvent.Metadata
                .FirstOrDefault(kv => string.Equals(NormalizeKey(kv.Key), candidate, StringComparison.OrdinalIgnoreCase));
            if (!match.Equals(default(KeyValuePair<string, object>)))
            {
                return match.Value;
            }
        }

        return null;
    }

    private object? GetDictionaryValue(Dictionary<string, object> dictionary, string key)
    {
        if (dictionary.Count == 0)
        {
            return null;
        }

        if (dictionary.TryGetValue(key, out var value))
        {
            return value;
        }

        var normalizedKey = NormalizeKey(key);
        foreach (var item in dictionary)
        {
            if (NormalizeKey(item.Key) == normalizedKey)
            {
                return item.Value;
            }
        }

        return null;
    }

    private List<MatchedEvent> ApplyAggregationWindow(AggregationWindow aggregation, List<MatchedEvent> events)
    {
        if (aggregation.Size <= 0)
        {
            return events;
        }

        var window = GetTimeSpan(aggregation.Size, aggregation.Unit);
        if (window is null)
        {
            return events;
        }

        var latestTimestamp = events.Max(e => e.Envelope.Normalized!.Timestamp);
        var threshold = latestTimestamp - window.Value;

        return events
            .Where(e => e.Envelope.Normalized!.Timestamp >= threshold)
            .ToList();
    }

    private TimeSpan? GetTimeSpan(int size, TimeUnit unit)
    {
        return unit switch
        {
            TimeUnit.Seconds => TimeSpan.FromSeconds(size),
            TimeUnit.Minutes => TimeSpan.FromMinutes(size),
            TimeUnit.Hours => TimeSpan.FromHours(size),
            TimeUnit.Days => TimeSpan.FromDays(size),
            _ => null
        };
    }

    private EvaluationResult EvaluateAggregation(AggregationWindow aggregation, List<MatchedEvent> events)
    {
        var groups = GroupEvents(events, aggregation.GroupBy);
        foreach (var group in groups)
        {
            var metrics = ComputeAggregate(aggregation, group.Events);
            if (metrics is null)
            {
                continue;
            }

            var havingSatisfied = aggregation.Having is null
                ? metrics.Value > 0
                : EvaluateHaving(aggregation.Having, metrics.Value);

            if (!havingSatisfied)
            {
                continue;
            }

            var context = BuildAggregationContext(group.Events, aggregation.GroupBy, metrics);
            var matchedConditions = group.Events
                .SelectMany(e => e.MatchedConditions)
                .Distinct()
                .ToList();

            return new EvaluationResult
            {
                IsMatch = true,
                ShouldTriggerAlert = true,
                Reason = aggregation.Having is null
                    ? $"Aggregation matched with value {metrics.Value}"
                    : $"Aggregation threshold met: {metrics.FieldName} = {metrics.Value}",
                MatchedConditions = matchedConditions,
                Context = context,
                AggregationCount = metrics.EventCount,
                AggregatedValue = metrics.Value
            };
        }

        return new EvaluationResult
        {
            IsMatch = false,
            ShouldTriggerAlert = false,
            Reason = "Aggregation threshold not met",
            AggregationCount = events.Count,
            MatchedConditions = events.SelectMany(e => e.MatchedConditions).Distinct().ToList()
        };
    }

    private IEnumerable<(string Key, List<MatchedEvent> Events)> GroupEvents(List<MatchedEvent> events, List<string>? groupByFields)
    {
        if (groupByFields is null || groupByFields.Count == 0)
        {
            yield return ("all", events);
            yield break;
        }

        foreach (var grouping in events.GroupBy(evt => BuildGroupKey(evt.Envelope, groupByFields)))
        {
            yield return (grouping.Key, grouping.ToList());
        }
    }

    private string BuildGroupKey(EventEnvelope envelope, List<string> groupByFields)
    {
        var parts = new List<string>(groupByFields.Count);
        foreach (var field in groupByFields)
        {
            var value = GetFieldValue(envelope, field);
            parts.Add(value?.ToString() ?? "null");
        }

        return string.Join("|", parts);
    }

    private AggregateMetrics? ComputeAggregate(AggregationWindow aggregation, List<MatchedEvent> events)
    {
        if (events.Count == 0)
        {
            return null;
        }

        var fieldName = aggregation.Having?.Field ?? "count";
        double value;

        switch (aggregation.Type)
        {
            case AggregationType.Count:
            case AggregationType.TimeWindow:
                fieldName = "count";
                value = events.Count;
                break;
            case AggregationType.Sum:
                value = events.Sum(e => GetNumericField(e.Envelope, fieldName));
                break;
            case AggregationType.Average:
                var avgValues = events.Select(e => GetNumericField(e.Envelope, fieldName)).ToList();
                if (avgValues.Count == 0)
                {
                    return null;
                }
                value = avgValues.Average();
                break;
            case AggregationType.Min:
                var minValues = events.Select(e => GetNumericField(e.Envelope, fieldName)).ToList();
                if (minValues.Count == 0)
                {
                    return null;
                }
                value = minValues.Min();
                break;
            case AggregationType.Max:
                var maxValues = events.Select(e => GetNumericField(e.Envelope, fieldName)).ToList();
                if (maxValues.Count == 0)
                {
                    return null;
                }
                value = maxValues.Max();
                break;
            default:
                fieldName = "count";
                value = events.Count;
                break;
        }

        return new AggregateMetrics(fieldName, value, events.Count);
    }

    private double GetNumericField(EventEnvelope envelope, string field)
    {
        var value = GetFieldValue(envelope, field) ?? 0;
        return ConvertToDouble(value);
    }

    private bool EvaluateHaving(Condition having, double aggregateValue)
    {
        var comparisonValue = ConvertToDouble(having.Value);

        return having.Operator switch
        {
            ConditionOperator.GreaterThan => aggregateValue > comparisonValue,
            ConditionOperator.GreaterThanOrEqual => aggregateValue >= comparisonValue,
            ConditionOperator.LessThan => aggregateValue < comparisonValue,
            ConditionOperator.LessThanOrEqual => aggregateValue <= comparisonValue,
            ConditionOperator.Equals => Math.Abs(aggregateValue - comparisonValue) < double.Epsilon,
            ConditionOperator.NotEquals => Math.Abs(aggregateValue - comparisonValue) > double.Epsilon,
            ConditionOperator.In => IsInList(aggregateValue, having.Value, having.CaseSensitive),
            ConditionOperator.NotIn => !IsInList(aggregateValue, having.Value, having.CaseSensitive),
            ConditionOperator.Exists => aggregateValue > 0,
            ConditionOperator.NotExists => aggregateValue <= 0,
            _ => false
        };
    }

    private Dictionary<string, object> BuildAggregationContext(IEnumerable<MatchedEvent> events, List<string>? groupByFields, AggregateMetrics metrics)
    {
        var firstEvent = events.First().Envelope;
        var context = BuildContext(firstEvent);

        context["count"] = metrics.EventCount;
        context[metrics.FieldName] = metrics.Value;

        if (groupByFields is not null)
        {
            foreach (var field in groupByFields)
            {
                var value = GetFieldValue(firstEvent, field);
                if (value is not null)
                {
                    context[field] = value;
                }
            }
        }

        return context;
    }

    private Dictionary<string, object> BuildContext(EventEnvelope eventEnvelope)
    {
        var context = new Dictionary<string, object>();

        if (eventEnvelope.Normalized is null)
        {
            return context;
        }

        context["sourceIp"] = eventEnvelope.Normalized.SourceIp;
        context["destinationIp"] = eventEnvelope.Normalized.DestinationIp;
        context["timestamp"] = eventEnvelope.Normalized.Timestamp;
        context["eventType"] = eventEnvelope.Normalized.EventType.ToString();

        foreach (var metadata in eventEnvelope.Normalized.Metadata)
        {
            context[metadata.Key] = metadata.Value;
        }

        foreach (var enrichment in eventEnvelope.Enrichment)
        {
            context[$"enrichment.{enrichment.Key}"] = enrichment.Value;
        }

        return context;
    }

    private bool CompareValues(object? fieldValue, object? comparisonValue, bool caseSensitive)
    {
        if (fieldValue is null || comparisonValue is null)
        {
            return fieldValue is null && comparisonValue is null;
        }

        if (TryConvertToDouble(fieldValue, out var fieldNumeric) && TryConvertToDouble(comparisonValue, out var comparisonNumeric))
        {
            return Math.Abs(fieldNumeric - comparisonNumeric) < double.Epsilon;
        }

        var fieldString = fieldValue.ToString() ?? string.Empty;
        var comparisonString = comparisonValue.ToString() ?? string.Empty;

        return caseSensitive
            ? fieldString.Equals(comparisonString, StringComparison.Ordinal)
            : fieldString.Equals(comparisonString, StringComparison.OrdinalIgnoreCase);
    }

    private bool ContainsValue(object? fieldValue, object? comparisonValue, bool caseSensitive)
    {
        if (fieldValue is null || comparisonValue is null)
        {
            return false;
        }

        var fieldString = fieldValue.ToString() ?? string.Empty;
        var comparisonString = comparisonValue.ToString() ?? string.Empty;

        return caseSensitive
            ? fieldString.Contains(comparisonString, StringComparison.Ordinal)
            : fieldString.Contains(comparisonString, StringComparison.OrdinalIgnoreCase);
    }

    private bool StartsWithValue(object? fieldValue, object? comparisonValue, bool caseSensitive)
    {
        if (fieldValue is null || comparisonValue is null)
        {
            return false;
        }

        var fieldString = fieldValue.ToString() ?? string.Empty;
        var comparisonString = comparisonValue.ToString() ?? string.Empty;

        return caseSensitive
            ? fieldString.StartsWith(comparisonString, StringComparison.Ordinal)
            : fieldString.StartsWith(comparisonString, StringComparison.OrdinalIgnoreCase);
    }

    private bool EndsWithValue(object? fieldValue, object? comparisonValue, bool caseSensitive)
    {
        if (fieldValue is null || comparisonValue is null)
        {
            return false;
        }

        var fieldString = fieldValue.ToString() ?? string.Empty;
        var comparisonString = comparisonValue.ToString() ?? string.Empty;

        return caseSensitive
            ? fieldString.EndsWith(comparisonString, StringComparison.Ordinal)
            : fieldString.EndsWith(comparisonString, StringComparison.OrdinalIgnoreCase);
    }

    private bool CompareNumeric(object? fieldValue, object? comparisonValue, Func<double, double, bool> comparison)
    {
        if (!TryConvertToDouble(fieldValue, out var fieldNumeric) || !TryConvertToDouble(comparisonValue, out var comparisonNumeric))
        {
            return false;
        }

        return comparison(fieldNumeric, comparisonNumeric);
    }

    private bool IsInList(object? fieldValue, object? listValue, bool caseSensitive)
    {
        if (fieldValue is null || listValue is null)
        {
            return false;
        }

        var candidates = EnumerateConditionValues(listValue);
        var fieldIsNumeric = TryConvertToDouble(fieldValue, out var fieldNumeric);
        var fieldString = fieldValue.ToString() ?? string.Empty;

        foreach (var candidate in candidates)
        {
            if (fieldIsNumeric && TryConvertToDouble(candidate, out var candidateNumeric))
            {
                if (Math.Abs(fieldNumeric - candidateNumeric) < double.Epsilon)
                {
                    return true;
                }
            }
            else
            {
                var candidateString = candidate?.ToString() ?? string.Empty;
                if (caseSensitive)
                {
                    if (fieldString.Equals(candidateString, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                else
                {
                    if (fieldString.Equals(candidateString, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private IEnumerable<object?> EnumerateConditionValues(object conditionValue)
    {
        switch (conditionValue)
        {
            case JsonElement json when json.ValueKind == JsonValueKind.Array:
                foreach (var element in json.EnumerateArray())
                {
                    yield return JsonElementToObject(element);
                }
                yield break;
            case JsonElement json:
                yield return JsonElementToObject(json);
                yield break;
            case IEnumerable<object?> enumerable:
                foreach (var item in enumerable)
                {
                    yield return item;
                }
                yield break;
            default:
                yield return conditionValue;
                yield break;
        }
    }

    private object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.TryGetDouble(out var doubleValue) ? doubleValue : null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private bool MatchesRegex(object? fieldValue, object? patternValue)
    {
        if (fieldValue is null || patternValue is null)
        {
            return false;
        }

        var input = fieldValue.ToString() ?? string.Empty;
        var pattern = patternValue.ToString() ?? string.Empty;

        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate regex pattern: {Pattern}", pattern);
            return false;
        }
    }

    private IEnumerable<string> GenerateKeyVariants(string key)
    {
        yield return key;
        yield return key.Replace("_", string.Empty);
        yield return ToCamelCase(key);
        yield return ToPascalCase(key);
        yield return ToSnakeCase(key);
    }

    private string NormalizeKey(string value)
    {
        return value
            .Replace(".", "_")
            .Replace("-", "_")
            .Replace(" ", "_")
            .Trim()
            .ToLowerInvariant();
    }

    private string ToCamelCase(string value)
    {
        var pascal = ToPascalCase(value);
        return string.IsNullOrEmpty(pascal) ? pascal : char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    private string ToPascalCase(string value)
    {
        var parts = value.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private string ToSnakeCase(string value)
    {
        var chars = new List<char>(value.Length * 2);
        foreach (var ch in value)
        {
            if (char.IsUpper(ch))
            {
                if (chars.Count > 0)
                {
                    chars.Add('_');
                }
                chars.Add(char.ToLowerInvariant(ch));
            }
            else if (ch == '-' || ch == ' ')
            {
                chars.Add('_');
            }
            else
            {
                chars.Add(ch);
            }
        }
        return new string(chars.ToArray());
    }

    private EventType ConvertEventTypeString(string eventTypeString)
    {
        return eventTypeString switch
        {
            "dnsquery" => EventType.DnsQuery,
            "dns_query" => EventType.DnsQuery,
            "authenticationattempt" => EventType.AuthenticationAttempt,
            "authentication_attempt" => EventType.AuthenticationAttempt,
            "authenticationfailure" => EventType.AuthenticationAttempt,
            "authentication_failure" => EventType.AuthenticationAttempt,
            "fileaccess" => EventType.FileAccess,
            "file_access" => EventType.FileAccess,
            "processexecution" => EventType.ProcessExecution,
            "process_execution" => EventType.ProcessExecution,
            "networktraffic" => EventType.NetworkTraffic,
            "network_traffic" => EventType.NetworkTraffic,
            "httprequest" => EventType.HttpRequest,
            "http_request" => EventType.HttpRequest,
            _ => EventType.Unknown
        };
    }

    private bool TryConvertToDouble(object? value, out double number)
    {
        switch (value)
        {
            case null:
                number = 0;
                return false;
            case double d:
                number = d;
                return true;
            case float f:
                number = f;
                return true;
            case long l:
                number = l;
                return true;
            case int i:
                number = i;
                return true;
            case decimal dec:
                number = (double)dec;
                return true;
            case JsonElement json when json.ValueKind == JsonValueKind.Number:
                number = json.GetDouble();
                return true;
            default:
                return double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number);
        }
    }

    private double ConvertToDouble(object? value)
    {
        return TryConvertToDouble(value, out var number) ? number : 0d;
    }
}
