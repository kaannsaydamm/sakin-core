using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sakin.Correlation.Exceptions;
using Sakin.Correlation.Parsers;
using Sakin.Correlation.Validation;
using Xunit;

namespace Sakin.Correlation.Tests.Integration;

public class RuleParserIntegrationTests
{
    private readonly Mock<ILogger<RuleValidator>> _mockValidatorLogger;
    private readonly Mock<ILogger<RuleParser>> _mockParserLogger;
    private readonly RuleValidator _validator;
    private readonly RuleParser _parser;

    public RuleParserIntegrationTests()
    {
        _mockValidatorLogger = new Mock<ILogger<RuleValidator>>();
        _mockParserLogger = new Mock<ILogger<RuleParser>>();
        _validator = new RuleValidator(_mockValidatorLogger.Object);
        _parser = new RuleParser(_validator, _mockParserLogger.Object);
    }

    [Fact]
    public async Task ParseRuleFromFileAsync_WithFailedLoginRule_ShouldParseSuccessfully()
    {
        // Arrange
        var rulePath = Path.Combine(
            Directory.GetCurrentDirectory(), 
            "..", "..", "..", "..", "..", 
            "configs", "rules", "failed-login-attempts.json");

        if (!File.Exists(rulePath))
        {
            // Try alternative path
            rulePath = Path.Combine(
                Directory.GetCurrentDirectory(), 
                "..", "..", "..", "..", "..", "..", 
                "configs", "rules", "failed-login-attempts.json");
        }

        if (!File.Exists(rulePath))
        {
            // Skip test if file doesn't exist
            return;
        }

        // Act
        var result = await _parser.ParseRuleFromFileAsync(rulePath);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("failed-login-attempts");
        result.Name.Should().Be("Multiple Failed Login Attempts");
        result.Severity.Should().Be(Sakin.Correlation.Models.SeverityLevel.Medium);
        result.Triggers.Should().HaveCount(1);
        result.Triggers[0].EventType.Should().Be("authentication_failure");
        result.Conditions.Should().HaveCount(3);
        result.Aggregation.Should().NotBeNull();
        result.Actions.Should().HaveCount(2);
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("category");
        result.Metadata["category"].Should().Be("authentication");
    }

    [Fact]
    public async Task ParseRuleFromFileAsync_WithSuspiciousFileAccessRule_ShouldParseSuccessfully()
    {
        // Arrange
        var rulePath = Path.Combine(
            Directory.GetCurrentDirectory(), 
            "..", "..", "..", "..", "..", 
            "configs", "rules", "suspicious-file-access.json");

        if (!File.Exists(rulePath))
        {
            // Try alternative path
            rulePath = Path.Combine(
                Directory.GetCurrentDirectory(), 
                "..", "..", "..", "..", "..", "..", 
                "configs", "rules", "suspicious-file-access.json");
        }

        if (!File.Exists(rulePath))
        {
            // Skip test if file doesn't exist
            return;
        }

        // Act
        var result = await _parser.ParseRuleFromFileAsync(rulePath);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("suspicious-file-access");
        result.Name.Should().Be("Suspicious File Access Pattern");
        result.Severity.Should().Be(Sakin.Correlation.Models.SeverityLevel.High);
        result.Triggers.Should().HaveCount(1);
        result.Triggers[0].EventType.Should().Be("file_access");
        result.Conditions.Should().HaveCount(3);
        result.Aggregation.Should().BeNull(); // This rule doesn't have aggregation
        result.Actions.Should().HaveCount(2);
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("mitre_tactic");
        result.Metadata["mitre_tactic"].Should().Be("Collection");
    }

    [Fact]
    public async Task ParseRuleFromFileAsync_WithMalwareDetectionRule_ShouldParseSuccessfully()
    {
        // Arrange
        var rulePath = Path.Combine(
            Directory.GetCurrentDirectory(), 
            "..", "..", "..", "..", "..", 
            "configs", "rules", "malware-detection.json");

        if (!File.Exists(rulePath))
        {
            // Try alternative path
            rulePath = Path.Combine(
                Directory.GetCurrentDirectory(), 
                "..", "..", "..", "..", "..", "..", 
                "configs", "rules", "malware-detection.json");
        }

        if (!File.Exists(rulePath))
        {
            // Skip test if file doesn't exist
            return;
        }

        // Act
        var result = await _parser.ParseRuleFromFileAsync(rulePath);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("malware-detection");
        result.Name.Should().Be("Malware Detection Based on Process Behavior");
        result.Severity.Should().Be(Sakin.Correlation.Models.SeverityLevel.Critical);
        result.Triggers.Should().HaveCount(1);
        result.Triggers[0].EventType.Should().Be("process_execution");
        result.Conditions.Should().HaveCount(3);
        result.Aggregation.Should().NotBeNull();
        result.Aggregation!.Type.Should().Be(Sakin.Correlation.Models.AggregationType.TimeWindow);
        result.Actions.Should().HaveCount(3);
        result.Actions.Should().Contain(a => a.Type == Sakin.Correlation.Models.ActionType.Quarantine);
        result.Actions.Should().Contain(a => a.Type == Sakin.Correlation.Models.ActionType.Block);
    }

    [Fact]
    public async Task ParseRuleFromFileAsync_WithInvalidRule_ShouldThrowException()
    {
        // Arrange
        var rulePath = Path.Combine(
            Directory.GetCurrentDirectory(), 
            "..", "..", "..", "..", "..", 
            "configs", "rules", "invalid-rule-example.json");

        if (!File.Exists(rulePath))
        {
            // Try alternative path
            rulePath = Path.Combine(
                Directory.GetCurrentDirectory(), 
                "..", "..", "..", "..", "..", "..", 
                "configs", "rules", "invalid-rule-example.json");
        }

        if (!File.Exists(rulePath))
        {
            // Skip test if file doesn't exist
            return;
        }

        // Act & Assert
        await _parser.Invoking(p => p.ParseRuleFromFileAsync(rulePath))
            .Should().ThrowAsync<RuleParsingException>();
    }

    [Fact]
    public async Task ParseRulesFromDirectoryAsync_WithConfigsDirectory_ShouldParseValidRules()
    {
        // Arrange
        var configsPath = Path.Combine(
            Directory.GetCurrentDirectory(), 
            "..", "..", "..", "..", "..", 
            "configs", "rules");

        if (!Directory.Exists(configsPath))
        {
            // Try alternative path
            configsPath = Path.Combine(
                Directory.GetCurrentDirectory(), 
                "..", "..", "..", "..", "..", "..", 
                "configs", "rules");
        }

        if (!Directory.Exists(configsPath))
        {
            // Skip test if directory doesn't exist
            return;
        }

        // Act
        var result = await _parser.ParseRulesFromDirectoryAsync(configsPath);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterOrEqualTo(3); // At least 3 valid rules
        
        // Check that specific rules are present
        result.Should().Contain(r => r.Id == "failed-login-attempts");
        result.Should().Contain(r => r.Id == "suspicious-file-access");
        result.Should().Contain(r => r.Id == "malware-detection");
        
        // All rules should have required fields
        foreach (var rule in result)
        {
            rule.Id.Should().NotBeNullOrEmpty();
            rule.Name.Should().NotBeNullOrEmpty();
            rule.Triggers.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task SerializeRule_WithParsedRule_ShouldRoundTripSuccessfully()
    {
        // Arrange
        var originalRule = new Sakin.Correlation.Models.CorrelationRule
        {
            Id = "roundtrip-test",
            Name = "Roundtrip Test",
            Description = "Testing serialization roundtrip",
            Enabled = true,
            Severity = Sakin.Correlation.Models.SeverityLevel.High,
            Triggers = new List<Sakin.Correlation.Models.Trigger>
            {
                new Sakin.Correlation.Models.Trigger
                {
                    Type = Sakin.Correlation.Models.TriggerType.Event,
                    EventType = "test_event",
                    Source = "test_source",
                    Filters = new Dictionary<string, object>
                    {
                        { "test_filter", "test_value" }
                    }
                }
            },
            Conditions = new List<Sakin.Correlation.Models.Condition>
            {
                new Sakin.Correlation.Models.Condition
                {
                    Field = "test_field",
                    Operator = Sakin.Correlation.Models.ConditionOperator.Equals,
                    Value = "test_value",
                    CaseSensitive = false,
                    Negate = true
                }
            },
            Aggregation = new Sakin.Correlation.Models.AggregationWindow
            {
                Type = Sakin.Correlation.Models.AggregationType.TimeWindow,
                Size = 5,
                Unit = Sakin.Correlation.Models.TimeUnit.Minutes,
                GroupBy = new List<string> { "source_ip" }
            },
            Actions = new List<Sakin.Correlation.Models.Action>
            {
                new Sakin.Correlation.Models.Action
                {
                    Type = Sakin.Correlation.Models.ActionType.Alert,
                    Parameters = new Dictionary<string, object>
                    {
                        { "title", "Test Alert" },
                        { "message", "Test message" }
                    }
                }
            },
            Metadata = new Dictionary<string, object>
            {
                { "test_key", "test_value" },
                { "test_number", 42 }
            }
        };

        // Act
        var serialized = _parser.SerializeRule(originalRule);
        
        // Parse it back to verify roundtrip
        var parsed = await _parser.ParseRuleAsync(serialized);

        // Assert
        parsed.Should().NotBeNull();
        parsed.Id.Should().Be(originalRule.Id);
        parsed.Name.Should().Be(originalRule.Name);
        parsed.Description.Should().Be(originalRule.Description);
        parsed.Enabled.Should().Be(originalRule.Enabled);
        parsed.Severity.Should().Be(originalRule.Severity);
        
        parsed.Triggers.Should().HaveCount(1);
        parsed.Triggers[0].Type.Should().Be(originalRule.Triggers[0].Type);
        parsed.Triggers[0].EventType.Should().Be(originalRule.Triggers[0].EventType);
        parsed.Triggers[0].Source.Should().Be(originalRule.Triggers[0].Source);
        
        parsed.Conditions.Should().HaveCount(1);
        parsed.Conditions[0].Field.Should().Be(originalRule.Conditions[0].Field);
        parsed.Conditions[0].Operator.Should().Be(originalRule.Conditions[0].Operator);
        parsed.Conditions[0].Value.Should().Be(originalRule.Conditions[0].Value);
        
        parsed.Aggregation.Should().NotBeNull();
        parsed.Aggregation!.Type.Should().Be(originalRule.Aggregation.Type);
        parsed.Aggregation.Size.Should().Be(originalRule.Aggregation.Size);
        parsed.Aggregation.Unit.Should().Be(originalRule.Aggregation.Unit);
        
        parsed.Actions.Should().HaveCount(1);
        parsed.Actions[0].Type.Should().Be(originalRule.Actions[0].Type);
        
        parsed.Metadata.Should().NotBeNull();
        parsed.Metadata.Should().ContainKey("test_key");
        parsed.Metadata.Should().ContainKey("test_number");
    }
}