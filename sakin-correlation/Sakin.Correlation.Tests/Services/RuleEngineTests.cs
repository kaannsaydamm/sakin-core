using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sakin.Common.Models;
using Sakin.Correlation.Models;
using Sakin.Correlation.Services;
using Xunit;

namespace Sakin.Correlation.Tests.Services;

public class RuleEngineTests
{
    private readonly Mock<IRuleProvider> _mockRuleProvider;
    private readonly Mock<IStateManager> _mockStateManager;
    private readonly Mock<ILogger<RuleEngine>> _mockLogger;
    private readonly RuleEngine _ruleEngine;

    public RuleEngineTests()
    {
        _mockRuleProvider = new Mock<IRuleProvider>();
        _mockStateManager = new Mock<IStateManager>();
        _mockLogger = new Mock<ILogger<RuleEngine>>();
        _ruleEngine = new RuleEngine(_mockRuleProvider.Object, _mockStateManager.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task EvaluateEventAsync_NoNormalizedData_ReturnsEmpty()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            Normalized = null
        };

        var rules = new List<CorrelationRule>
        {
            new()
            {
                Id = "test-rule",
                Name = "Test Rule",
                Enabled = true,
                MinEventCount = 2
            }
        };
        _mockRuleProvider.Setup(x => x.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        var result = await _ruleEngine.EvaluateEventAsync(envelope, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateEventAsync_RuleNotEnabled_NoAlertGenerated()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                SourceIp = "192.168.1.1",
                EventType = EventType.NetworkTraffic
            }
        };

        var rules = new List<CorrelationRule>
        {
            new()
            {
                Id = "test-rule",
                Name = "Test Rule",
                Enabled = false,
                MinEventCount = 1
            }
        };
        _mockRuleProvider.Setup(x => x.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        var result = await _ruleEngine.EvaluateEventAsync(envelope, CancellationToken.None);

        result.Should().BeEmpty();
        _mockStateManager.Verify(x => x.AddEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateEventAsync_EventCountBelowThreshold_NoAlertGenerated()
    {
        var eventId = Guid.NewGuid();
        var envelope = new EventEnvelope
        {
            EventId = eventId,
            Normalized = new NormalizedEvent
            {
                Id = eventId,
                SourceIp = "192.168.1.1",
                EventType = EventType.NetworkTraffic
            }
        };

        var rules = new List<CorrelationRule>
        {
            new()
            {
                Id = "test-rule",
                Name = "Test Rule",
                Enabled = true,
                MinEventCount = 5,
                TimeWindowSeconds = 300
            }
        };
        _mockRuleProvider.Setup(x => x.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        _mockStateManager.Setup(x => x.AddEventAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CorrelationState { EventCount = 2, EventIds = new List<Guid> { eventId } });

        var result = await _ruleEngine.EvaluateEventAsync(envelope, CancellationToken.None);

        result.Should().BeEmpty();
        _mockStateManager.Verify(x => x.ClearGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateEventAsync_EventCountReachesThreshold_AlertGenerated()
    {
        var eventId1 = Guid.NewGuid();
        var eventId2 = Guid.NewGuid();
        var envelope = new EventEnvelope
        {
            EventId = eventId2,
            Normalized = new NormalizedEvent
            {
                Id = eventId2,
                SourceIp = "192.168.1.1",
                EventType = EventType.NetworkTraffic,
                Severity = Severity.Medium
            }
        };

        var rules = new List<CorrelationRule>
        {
            new()
            {
                Id = "test-rule",
                Name = "Test Rule",
                Description = "Test Description",
                Severity = Severity.High,
                Enabled = true,
                MinEventCount = 2,
                TimeWindowSeconds = 300,
                Tags = new List<string> { "test-tag" }
            }
        };
        _mockRuleProvider.Setup(x => x.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        _mockStateManager.Setup(x => x.AddEventAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CorrelationState
            {
                EventCount = 2,
                EventIds = new List<Guid> { eventId1, eventId2 }
            });

        var result = (await _ruleEngine.EvaluateEventAsync(envelope, CancellationToken.None)).ToList();

        result.Should().HaveCount(1);
        var alert = result.First();
        alert.RuleName.Should().Be("Test Rule");
        alert.Severity.Should().Be(Severity.High);
        alert.EventCount.Should().Be(2);
        alert.EventIds.Should().Contain(eventId1);
        alert.EventIds.Should().Contain(eventId2);
        alert.SourceIp.Should().Be("192.168.1.1");
        alert.Tags.Should().Contain("test-tag");

        _mockStateManager.Verify(x => x.ClearGroupAsync("test-rule", "192.168.1.1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateEventAsync_ConditionNotMatched_NoAlertGenerated()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                SourceIp = "192.168.1.1",
                EventType = EventType.NetworkTraffic
            }
        };

        var rules = new List<CorrelationRule>
        {
            new()
            {
                Id = "test-rule",
                Name = "Test Rule",
                Enabled = true,
                MinEventCount = 1,
                Conditions = new List<RuleCondition>
                {
                    new() { Field = "EventType", Operator = RuleOperator.Equals, Value = nameof(EventType.HttpRequest) }
                }
            }
        };
        _mockRuleProvider.Setup(x => x.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        var result = await _ruleEngine.EvaluateEventAsync(envelope, CancellationToken.None);

        result.Should().BeEmpty();
        _mockStateManager.Verify(x => x.AddEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
