using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sakin.Correlation.Engine;
using Sakin.Correlation.Models;
using Sakin.Common.Models;
using Xunit;

namespace Sakin.Correlation.Tests.Engine;

public class RuleAggregationTests
{
    private readonly Mock<ILogger<RuleEvaluator>> _mockLogger;
    private readonly RuleEvaluator _evaluator;

    public RuleAggregationTests()
    {
        _mockLogger = new Mock<ILogger<RuleEvaluator>>();
        _evaluator = new RuleEvaluator(_mockLogger.Object);
    }

    [Fact]
    public async Task AggregationWithCount_WhenThresholdMet_ShouldTriggerAlert()
    {
        var rule = new CorrelationRule
        {
            Id = "test-aggregation",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "authentication_attempt" }
            },
            Conditions = new List<Condition>
            {
                new Condition { Field = "username", Operator = ConditionOperator.Exists }
            },
            Aggregation = new AggregationWindow
            {
                Type = AggregationType.Count,
                Size = 5,
                Unit = TimeUnit.Minutes,
                GroupBy = new List<string> { "username", "source_ip" },
                Having = new Condition
                {
                    Field = "count",
                    Operator = ConditionOperator.GreaterThanOrEqual,
                    Value = 3
                }
            }
        };

        var events = CreateAuthenticationEventStream("testuser", "192.168.1.100", 5);

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, events);

        result.IsMatch.Should().BeTrue();
        result.ShouldTriggerAlert.Should().BeTrue();
        result.AggregationCount.Should().Be(5);
        result.Context.Should().ContainKey("username");
        result.Context["username"].Should().Be("testuser");
    }

    [Fact]
    public async Task AggregationWithCount_WhenThresholdNotMet_ShouldNotTriggerAlert()
    {
        var rule = new CorrelationRule
        {
            Id = "test-aggregation",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "authentication_attempt" }
            },
            Conditions = new List<Condition>
            {
                new Condition { Field = "username", Operator = ConditionOperator.Exists }
            },
            Aggregation = new AggregationWindow
            {
                Type = AggregationType.Count,
                Size = 5,
                Unit = TimeUnit.Minutes,
                GroupBy = new List<string> { "username", "source_ip" },
                Having = new Condition
                {
                    Field = "count",
                    Operator = ConditionOperator.GreaterThanOrEqual,
                    Value = 10
                }
            }
        };

        var events = CreateAuthenticationEventStream("testuser", "192.168.1.100", 5);

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, events);

        result.ShouldTriggerAlert.Should().BeFalse();
        result.AggregationCount.Should().Be(5);
    }

    [Fact]
    public async Task AggregationWithGroupBy_ShouldSeparateGroups()
    {
        var rule = new CorrelationRule
        {
            Id = "test-aggregation",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "authentication_attempt" }
            },
            Conditions = new List<Condition>
            {
                new Condition { Field = "username", Operator = ConditionOperator.Exists }
            },
            Aggregation = new AggregationWindow
            {
                Type = AggregationType.Count,
                Size = 5,
                Unit = TimeUnit.Minutes,
                GroupBy = new List<string> { "username" },
                Having = new Condition
                {
                    Field = "count",
                    Operator = ConditionOperator.GreaterThanOrEqual,
                    Value = 5
                }
            }
        };

        var events = new List<EventEnvelope>();
        events.AddRange(CreateAuthenticationEventStream("user1", "192.168.1.100", 3));
        events.AddRange(CreateAuthenticationEventStream("user2", "192.168.1.101", 3));

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, events);

        result.ShouldTriggerAlert.Should().BeFalse();
    }

    [Fact]
    public async Task AggregationWithoutGroupBy_ShouldAggregateAll()
    {
        var rule = new CorrelationRule
        {
            Id = "test-aggregation",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "authentication_attempt" }
            },
            Conditions = new List<Condition>
            {
                new Condition { Field = "username", Operator = ConditionOperator.Exists }
            },
            Aggregation = new AggregationWindow
            {
                Type = AggregationType.Count,
                Size = 5,
                Unit = TimeUnit.Minutes,
                Having = new Condition
                {
                    Field = "count",
                    Operator = ConditionOperator.GreaterThanOrEqual,
                    Value = 5
                }
            }
        };

        var events = new List<EventEnvelope>();
        events.AddRange(CreateAuthenticationEventStream("user1", "192.168.1.100", 2));
        events.AddRange(CreateAuthenticationEventStream("user2", "192.168.1.101", 2));
        events.AddRange(CreateAuthenticationEventStream("user3", "192.168.1.102", 2));

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, events);

        result.ShouldTriggerAlert.Should().BeTrue();
        result.AggregationCount.Should().Be(6);
    }

    [Fact]
    public async Task AggregationWithNoMatchingEvents_ShouldNotTriggerAlert()
    {
        var rule = new CorrelationRule
        {
            Id = "test-aggregation",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "dns_query" }
            },
            Conditions = new List<Condition>
            {
                new Condition { Field = "username", Operator = ConditionOperator.Exists }
            },
            Aggregation = new AggregationWindow
            {
                Type = AggregationType.Count,
                Size = 5,
                Unit = TimeUnit.Minutes,
                Having = new Condition
                {
                    Field = "count",
                    Operator = ConditionOperator.GreaterThanOrEqual,
                    Value = 3
                }
            }
        };

        var events = CreateAuthenticationEventStream("testuser", "192.168.1.100", 5);

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, events);

        result.IsMatch.Should().BeFalse();
        result.ShouldTriggerAlert.Should().BeFalse();
        result.Reason.Should().Contain("did not match");
    }

    [Fact]
    public async Task DeterministicEvaluation_WithSameInputs_ShouldProduceSameResults()
    {
        var rule = new CorrelationRule
        {
            Id = "test-deterministic",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "authentication_attempt" }
            },
            Conditions = new List<Condition>
            {
                new Condition 
                { 
                    Field = "metadata.failure_reason", 
                    Operator = ConditionOperator.Equals,
                    Value = "invalid_password"
                }
            },
            Aggregation = new AggregationWindow
            {
                Type = AggregationType.Count,
                Size = 5,
                Unit = TimeUnit.Minutes,
                GroupBy = new List<string> { "username" },
                Having = new Condition
                {
                    Field = "count",
                    Operator = ConditionOperator.GreaterThanOrEqual,
                    Value = 3
                }
            }
        };

        var events = CreateAuthenticationEventStream("admin", "192.168.1.100", 5);

        var result1 = await _evaluator.EvaluateWithAggregationAsync(rule, events);
        var result2 = await _evaluator.EvaluateWithAggregationAsync(rule, events);
        var result3 = await _evaluator.EvaluateWithAggregationAsync(rule, events);

        result1.IsMatch.Should().Be(result2.IsMatch);
        result1.IsMatch.Should().Be(result3.IsMatch);
        result1.ShouldTriggerAlert.Should().Be(result2.ShouldTriggerAlert);
        result1.ShouldTriggerAlert.Should().Be(result3.ShouldTriggerAlert);
        result1.AggregationCount.Should().Be(result2.AggregationCount);
        result1.AggregationCount.Should().Be(result3.AggregationCount);
    }

    [Fact]
    public async Task MultipleGroups_ShouldTriggerForFirstMatchingGroup()
    {
        var rule = new CorrelationRule
        {
            Id = "test-multi-group",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "authentication_attempt" }
            },
            Conditions = new List<Condition>
            {
                new Condition { Field = "username", Operator = ConditionOperator.Exists }
            },
            Aggregation = new AggregationWindow
            {
                Type = AggregationType.Count,
                Size = 5,
                Unit = TimeUnit.Minutes,
                GroupBy = new List<string> { "username" },
                Having = new Condition
                {
                    Field = "count",
                    Operator = ConditionOperator.GreaterThanOrEqual,
                    Value = 3
                }
            }
        };

        var events = new List<EventEnvelope>();
        events.AddRange(CreateAuthenticationEventStream("user1", "192.168.1.100", 2));
        events.AddRange(CreateAuthenticationEventStream("user2", "192.168.1.101", 5));
        events.AddRange(CreateAuthenticationEventStream("user3", "192.168.1.102", 2));

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, events);

        result.ShouldTriggerAlert.Should().BeTrue();
        result.Context["username"].Should().Be("user2");
    }

    [Fact]
    public async Task EmptyEventStream_ShouldNotTriggerAlert()
    {
        var rule = new CorrelationRule
        {
            Id = "test-empty",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "authentication_attempt" }
            },
            Aggregation = new AggregationWindow
            {
                Type = AggregationType.Count,
                Size = 5,
                Unit = TimeUnit.Minutes,
                Having = new Condition
                {
                    Field = "count",
                    Operator = ConditionOperator.GreaterThanOrEqual,
                    Value = 1
                }
            }
        };

        var events = new List<EventEnvelope>();

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, events);

        result.IsMatch.Should().BeFalse();
        result.Reason.Should().Be("No events to evaluate");
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    public async Task ThresholdTesting_WithVariousCounts_ShouldRespectThreshold(int eventCount, bool shouldAlert)
    {
        var rule = new CorrelationRule
        {
            Id = "test-threshold",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "authentication_attempt" }
            },
            Conditions = new List<Condition>
            {
                new Condition { Field = "username", Operator = ConditionOperator.Exists }
            },
            Aggregation = new AggregationWindow
            {
                Type = AggregationType.Count,
                Size = 5,
                Unit = TimeUnit.Minutes,
                GroupBy = new List<string> { "username" },
                Having = new Condition
                {
                    Field = "count",
                    Operator = ConditionOperator.GreaterThanOrEqual,
                    Value = 3
                }
            }
        };

        var events = CreateAuthenticationEventStream("testuser", "192.168.1.100", eventCount);

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, events);

        result.ShouldTriggerAlert.Should().Be(shouldAlert);
    }

    private List<EventEnvelope> CreateAuthenticationEventStream(string username, string sourceIp, int count)
    {
        var events = new List<EventEnvelope>();
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < count; i++)
        {
            events.Add(new EventEnvelope
            {
                EventId = Guid.NewGuid(),
                ReceivedAt = DateTimeOffset.UtcNow,
                Source = "test-source",
                SourceType = "test-sensor",
                Normalized = new NormalizedEvent
                {
                    Id = Guid.NewGuid(),
                    Timestamp = baseTime.AddSeconds(i * 10),
                    EventType = EventType.AuthenticationAttempt,
                    Severity = Severity.Medium,
                    SourceIp = sourceIp,
                    DestinationIp = "192.168.1.10",
                    Protocol = Protocol.TCP,
                    Metadata = new Dictionary<string, object>
                    {
                        { "username", username },
                        { "failure_reason", "invalid_password" },
                        { "success", false }
                    }
                }
            });
        }

        return events;
    }
}
