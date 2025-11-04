using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sakin.Correlation.Engine;
using Sakin.Correlation.Models;
using Sakin.Common.Models;
using Xunit;

namespace Sakin.Correlation.Tests.Engine;

public class RuleEvaluatorTests
{
    private readonly Mock<ILogger<RuleEvaluator>> _mockLogger;
    private readonly RuleEvaluator _evaluator;

    public RuleEvaluatorTests()
    {
        _mockLogger = new Mock<ILogger<RuleEvaluator>>();
        _evaluator = new RuleEvaluator(_mockLogger.Object);
    }

    [Fact]
    public async Task EvaluateAsync_WithDisabledRule_ShouldNotMatch()
    {
        var rule = new CorrelationRule
        {
            Id = "test-rule",
            Enabled = false,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "test_event" }
            }
        };

        var eventEnvelope = CreateTestEvent(EventType.NetworkTraffic);

        var result = await _evaluator.EvaluateAsync(rule, eventEnvelope);

        result.IsMatch.Should().BeFalse();
        result.Reason.Should().Be("Rule is disabled");
    }

    [Fact]
    public async Task EvaluateAsync_WithNoNormalizedData_ShouldNotMatch()
    {
        var rule = new CorrelationRule
        {
            Id = "test-rule",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "test_event" }
            }
        };

        var eventEnvelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "test-source",
            Normalized = null
        };

        var result = await _evaluator.EvaluateAsync(rule, eventEnvelope);

        result.IsMatch.Should().BeFalse();
        result.Reason.Should().Be("Event has no normalized data");
    }

    [Fact]
    public async Task EvaluateAsync_WithSimpleExistsCondition_ShouldMatch()
    {
        var rule = new CorrelationRule
        {
            Id = "test-rule",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "authentication_attempt" }
            },
            Conditions = new List<Condition>
            {
                new Condition { Field = "username", Operator = ConditionOperator.Exists }
            }
        };

        var eventEnvelope = CreateAuthenticationEvent("testuser", "192.168.1.100");

        var result = await _evaluator.EvaluateAsync(rule, eventEnvelope);

        result.IsMatch.Should().BeTrue();
        result.ShouldTriggerAlert.Should().BeTrue();
        result.MatchedConditions.Should().Contain("username Exists");
    }

    [Fact]
    public async Task EvaluateAsync_WithMismatchedTrigger_ShouldNotMatch()
    {
        var rule = new CorrelationRule
        {
            Id = "test-rule",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "dns_query" }
            },
            Conditions = new List<Condition>
            {
                new Condition { Field = "username", Operator = ConditionOperator.Exists }
            }
        };

        var eventEnvelope = CreateAuthenticationEvent("testuser", "192.168.1.100");

        var result = await _evaluator.EvaluateAsync(rule, eventEnvelope);

        result.IsMatch.Should().BeFalse();
        result.Reason.Should().Be("Event did not match trigger criteria");
    }

    [Fact]
    public async Task EvaluateAsync_WithEqualsCondition_ShouldMatch()
    {
        var rule = new CorrelationRule
        {
            Id = "test-rule",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "authentication_attempt" }
            },
            Conditions = new List<Condition>
            {
                new Condition 
                { 
                    Field = "username", 
                    Operator = ConditionOperator.Equals,
                    Value = "admin",
                    CaseSensitive = false
                }
            }
        };

        var eventEnvelope = CreateAuthenticationEvent("admin", "192.168.1.100");

        var result = await _evaluator.EvaluateAsync(rule, eventEnvelope);

        result.IsMatch.Should().BeTrue();
        result.ShouldTriggerAlert.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WithRegexCondition_ShouldMatch()
    {
        var rule = new CorrelationRule
        {
            Id = "test-rule",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "dns_query" }
            },
            Conditions = new List<Condition>
            {
                new Condition 
                { 
                    Field = "metadata.queryName",
                    Operator = ConditionOperator.Regex,
                    Value = ".*(malicious|evil).*"
                }
            }
        };

        var eventEnvelope = CreateDnsQueryEvent("malicious.com", "192.168.1.100");

        var result = await _evaluator.EvaluateAsync(rule, eventEnvelope);

        result.IsMatch.Should().BeTrue();
        result.ShouldTriggerAlert.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WithGreaterThanCondition_ShouldMatch()
    {
        var rule = new CorrelationRule
        {
            Id = "test-rule",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "network_traffic" }
            },
            Conditions = new List<Condition>
            {
                new Condition 
                { 
                    Field = "metadata.bytes_sent",
                    Operator = ConditionOperator.GreaterThan,
                    Value = 1000000
                }
            }
        };

        var eventEnvelope = CreateNetworkTrafficEvent("192.168.1.100", "8.8.8.8", 2000000);

        var result = await _evaluator.EvaluateAsync(rule, eventEnvelope);

        result.IsMatch.Should().BeTrue();
        result.ShouldTriggerAlert.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WithInListCondition_ShouldMatch()
    {
        var rule = new CorrelationRule
        {
            Id = "test-rule",
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
                    Operator = ConditionOperator.In,
                    Value = new List<object> { "invalid_password", "account_locked" }
                }
            }
        };

        var eventEnvelope = CreateAuthenticationFailureEvent("testuser", "192.168.1.100", "invalid_password");

        var result = await _evaluator.EvaluateAsync(rule, eventEnvelope);

        result.IsMatch.Should().BeTrue();
        result.ShouldTriggerAlert.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WithHourOfDayCondition_ShouldMatch()
    {
        var rule = new CorrelationRule
        {
            Id = "test-rule",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "file_access" }
            },
            Conditions = new List<Condition>
            {
                new Condition 
                { 
                    Field = "hour_of_day",
                    Operator = ConditionOperator.In,
                    Value = new List<object> { 0, 1, 2, 3 }
                }
            }
        };

        var eventEnvelope = CreateFileAccessEvent("/etc/passwd", "user1", hour: 2);

        var result = await _evaluator.EvaluateAsync(rule, eventEnvelope);

        result.IsMatch.Should().BeTrue();
        result.ShouldTriggerAlert.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WithNegatedCondition_ShouldMatch()
    {
        var rule = new CorrelationRule
        {
            Id = "test-rule",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "file_access" }
            },
            Conditions = new List<Condition>
            {
                new Condition 
                { 
                    Field = "metadata.user_role",
                    Operator = ConditionOperator.In,
                    Value = new List<object> { "administrator", "system" },
                    Negate = true
                }
            }
        };

        var eventEnvelope = CreateFileAccessEvent("/etc/passwd", "user1", userRole: "user");

        var result = await _evaluator.EvaluateAsync(rule, eventEnvelope);

        result.IsMatch.Should().BeTrue();
        result.ShouldTriggerAlert.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WithMultipleConditions_AllMustMatch()
    {
        var rule = new CorrelationRule
        {
            Id = "test-rule",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "authentication_attempt" }
            },
            Conditions = new List<Condition>
            {
                new Condition { Field = "username", Operator = ConditionOperator.Exists },
                new Condition { Field = "source_ip", Operator = ConditionOperator.Exists },
                new Condition 
                { 
                    Field = "metadata.failure_reason", 
                    Operator = ConditionOperator.In,
                    Value = new List<object> { "invalid_password" }
                }
            }
        };

        var eventEnvelope = CreateAuthenticationFailureEvent("testuser", "192.168.1.100", "invalid_password");

        var result = await _evaluator.EvaluateAsync(rule, eventEnvelope);

        result.IsMatch.Should().BeTrue();
        result.ShouldTriggerAlert.Should().BeTrue();
        result.MatchedConditions.Should().HaveCount(3);
    }

    [Fact]
    public async Task EvaluateAsync_WithOneFailedCondition_ShouldNotMatch()
    {
        var rule = new CorrelationRule
        {
            Id = "test-rule",
            Enabled = true,
            Triggers = new List<Trigger>
            {
                new Trigger { Type = TriggerType.Event, EventType = "authentication_attempt" }
            },
            Conditions = new List<Condition>
            {
                new Condition { Field = "username", Operator = ConditionOperator.Exists },
                new Condition 
                { 
                    Field = "metadata.failure_reason", 
                    Operator = ConditionOperator.Equals,
                    Value = "account_locked"
                }
            }
        };

        var eventEnvelope = CreateAuthenticationFailureEvent("testuser", "192.168.1.100", "invalid_password");

        var result = await _evaluator.EvaluateAsync(rule, eventEnvelope);

        result.IsMatch.Should().BeFalse();
        result.Reason.Should().Contain("Condition failed");
    }

    private EventEnvelope CreateTestEvent(EventType eventType)
    {
        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "test-source",
            SourceType = "test-sensor",
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                Severity = Severity.Info,
                SourceIp = "192.168.1.100",
                DestinationIp = "192.168.1.1",
                Protocol = Protocol.TCP
            }
        };
    }

    private EventEnvelope CreateAuthenticationEvent(string username, string sourceIp)
    {
        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "test-source",
            SourceType = "test-sensor",
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = EventType.AuthenticationAttempt,
                Severity = Severity.Info,
                SourceIp = sourceIp,
                DestinationIp = "192.168.1.10",
                Protocol = Protocol.TCP,
                Metadata = new Dictionary<string, object>
                {
                    { "username", username }
                }
            }
        };
    }

    private EventEnvelope CreateAuthenticationFailureEvent(string username, string sourceIp, string failureReason)
    {
        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "test-source",
            SourceType = "test-sensor",
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = EventType.AuthenticationAttempt,
                Severity = Severity.Medium,
                SourceIp = sourceIp,
                DestinationIp = "192.168.1.10",
                Protocol = Protocol.TCP,
                Metadata = new Dictionary<string, object>
                {
                    { "username", username },
                    { "failure_reason", failureReason },
                    { "success", false }
                }
            }
        };
    }

    private EventEnvelope CreateDnsQueryEvent(string queryName, string sourceIp)
    {
        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "test-source",
            SourceType = "network-sensor",
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = EventType.DnsQuery,
                Severity = Severity.Info,
                SourceIp = sourceIp,
                DestinationIp = "8.8.8.8",
                SourcePort = 54321,
                DestinationPort = 53,
                Protocol = Protocol.UDP,
                Metadata = new Dictionary<string, object>
                {
                    { "queryName", queryName },
                    { "queryType", "A" }
                }
            }
        };
    }

    private EventEnvelope CreateNetworkTrafficEvent(string sourceIp, string destIp, int bytesSent)
    {
        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "test-source",
            SourceType = "network-sensor",
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = EventType.NetworkTraffic,
                Severity = Severity.Info,
                SourceIp = sourceIp,
                DestinationIp = destIp,
                Protocol = Protocol.TCP,
                Metadata = new Dictionary<string, object>
                {
                    { "bytes_sent", bytesSent },
                    { "bytes_received", 1000 }
                }
            }
        };
    }

    private EventEnvelope CreateFileAccessEvent(string filePath, string username, int hour = 14, string userRole = "user")
    {
        var timestamp = new DateTime(2024, 1, 15, hour, 30, 0, DateTimeKind.Utc);
        
        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "test-source",
            SourceType = "file-sensor",
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = timestamp,
                EventType = EventType.FileAccess,
                Severity = Severity.Info,
                SourceIp = "192.168.1.100",
                DestinationIp = "192.168.1.10",
                Protocol = Protocol.Unknown,
                Metadata = new Dictionary<string, object>
                {
                    { "file_path", filePath },
                    { "username", username },
                    { "user_role", userRole },
                    { "action", "read" }
                }
            }
        };
    }
}
