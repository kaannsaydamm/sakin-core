using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sakin.Correlation.Exceptions;
using Sakin.Correlation.Models;
using Sakin.Correlation.Parsers;
using Sakin.Correlation.Validation;
using System.Text.Json;
using Xunit;

namespace Sakin.Correlation.Tests.Parsers;

public class RuleParserTests
{
    private readonly Mock<IRuleValidator> _mockValidator;
    private readonly Mock<ILogger<RuleParser>> _mockLogger;
    private readonly RuleParser _parser;

    public RuleParserTests()
    {
        _mockValidator = new Mock<IRuleValidator>();
        _mockLogger = new Mock<ILogger<RuleParser>>();
        _parser = new RuleParser(_mockValidator.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ParseRuleAsync_WithValidRule_ShouldReturnCorrelationRule()
    {
        // Arrange
        var ruleJson = @"{
            ""id"": ""test-rule"",
            ""name"": ""Test Rule"",
            ""severity"": ""medium"",
            ""triggers"": [
                {
                    ""type"": ""event"",
                    ""eventType"": ""test_event""
                }
            ]
        }";

        _mockValidator.Setup(v => v.ValidateRuleAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(new ValidationResult(true, "Valid")));

        // Act
        var result = await _parser.ParseRuleAsync(ruleJson);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("test-rule");
        result.Name.Should().Be("Test Rule");
        result.Severity.Should().Be(SeverityLevel.Medium);
        result.Triggers.Should().HaveCount(1);
        result.Triggers[0].EventType.Should().Be("test_event");
    }

    [Fact]
    public async Task ParseRuleAsync_WithInvalidJson_ShouldThrowRuleParsingException()
    {
        // Arrange
        var invalidJson = @"{ invalid json }";

        // Act & Assert
        await _parser.Invoking(p => p.ParseRuleAsync(invalidJson))
            .Should().ThrowAsync<RuleParsingException>()
            .WithMessage("*JSON parsing error*");
    }

    [Fact]
    public async Task ParseRuleAsync_WithValidationError_ShouldThrowRuleParsingException()
    {
        // Arrange
        var ruleJson = @"{
            ""id"": ""test-rule"",
            ""name"": ""Test Rule""
        }";

        _mockValidator.Setup(v => v.ValidateRuleAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(new ValidationResult(false, "Validation failed", "severity")));

        // Act & Assert
        await _parser.Invoking(p => p.ParseRuleAsync(ruleJson))
            .Should().ThrowAsync<RuleParsingException>()
            .WithMessage("severity*Validation failed*");
    }

    [Fact]
    public async Task ParseRuleFromFileAsync_WithValidFile_ShouldReturnCorrelationRule()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var ruleJson = @"{
            ""id"": ""file-rule"",
            ""name"": ""File Rule"",
            ""severity"": ""high"",
            ""triggers"": [
                {
                    ""type"": ""event"",
                    ""eventType"": ""file_event""
                }
            ]
        }";

        await File.WriteAllTextAsync(tempFile, ruleJson);
        _mockValidator.Setup(v => v.ValidateRuleAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(new ValidationResult(true, "Valid")));

        try
        {
            // Act
            var result = await _parser.ParseRuleFromFileAsync(tempFile);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be("file-rule");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseRuleFromFileAsync_WithNonExistentFile_ShouldThrowRuleParsingException()
    {
        // Arrange
        var nonExistentFile = "/path/to/non/existent/file.json";

        // Act & Assert
        await _parser.Invoking(p => p.ParseRuleFromFileAsync(nonExistentFile))
            .Should().ThrowAsync<RuleParsingException>()
            .WithMessage("*File not found*");
    }

    [Fact]
    public async Task ParseRulesFromDirectoryAsync_WithValidRules_ShouldReturnListOfRules()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var rule1Json = @"{
            ""id"": ""rule1"",
            ""name"": ""Rule 1"",
            ""severity"": ""low"",
            ""triggers"": [{""type"": ""event"", ""eventType"": ""event1""}]
        }";

        var rule2Json = @"{
            ""id"": ""rule2"",
            ""name"": ""Rule 2"",
            ""severity"": ""medium"",
            ""triggers"": [{""type"": ""event"", ""eventType"": ""event2""}]
        }";

        await File.WriteAllTextAsync(Path.Combine(tempDir, "rule1.json"), rule1Json);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "rule2.json"), rule2Json);

        _mockValidator.Setup(v => v.ValidateRuleAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(new ValidationResult(true, "Valid")));

        try
        {
            // Act
            var result = await _parser.ParseRulesFromDirectoryAsync(tempDir);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(r => r.Id == "rule1");
            result.Should().Contain(r => r.Id == "rule2");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ParseRulesFromDirectoryAsync_WithNonExistentDirectory_ShouldThrowRuleParsingException()
    {
        // Arrange
        var nonExistentDir = "/path/to/non/existent/directory";

        // Act & Assert
        await _parser.Invoking(p => p.ParseRulesFromDirectoryAsync(nonExistentDir))
            .Should().ThrowAsync<RuleParsingException>()
            .WithMessage("*Directory not found*");
    }

    [Fact]
    public async Task ParseRulesFromDirectoryAsync_WithMixedValidAndInvalidRules_ShouldReturnValidRulesAndLogErrors()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var validRuleJson = @"{
            ""id"": ""valid-rule"",
            ""name"": ""Valid Rule"",
            ""severity"": ""low"",
            ""triggers"": [{""type"": ""event"", ""eventType"": ""valid_event""}]
        }";

        var invalidRuleJson = @"{ invalid json }";

        await File.WriteAllTextAsync(Path.Combine(tempDir, "valid-rule.json"), validRuleJson);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "invalid-rule.json"), invalidRuleJson);

        _mockValidator.Setup(v => v.ValidateRuleAsync(It.IsAny<string>()))
            .Returns<string>(json => Task.FromResult(json.Contains("valid-rule") 
                ? new ValidationResult(true, "Valid")
                : new ValidationResult(false, "Invalid")));

        try
        {
            // Act
            var result = await _parser.ParseRulesFromDirectoryAsync(tempDir);

            // Assert
            result.Should().HaveCount(1);
            result[0].Id.Should().Be("valid-rule");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SerializeRule_WithValidRule_ShouldReturnJsonString()
    {
        // Arrange
        var rule = new CorrelationRule
        {
            Id = "serialize-test",
            Name = "Serialize Test",
            Severity = SeverityLevel.High,
            Triggers = new List<Trigger>
            {
                new Trigger
                {
                    Type = TriggerType.Event,
                    EventType = "test_event"
                }
            }
        };

        // Act
        var result = _parser.SerializeRule(rule);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("serialize-test");
        result.Should().Contain("Serialize Test");
        result.Should().Contain("high");
        result.Should().Contain("test_event");

        // Verify it's valid JSON
        var parsed = JsonSerializer.Deserialize<CorrelationRule>(result);
        parsed.Should().NotBeNull();
        parsed!.Id.Should().Be("serialize-test");
    }

    [Fact]
    public async Task ParseRuleAsync_WithDuplicateTriggerEventTypes_ShouldThrowRuleParsingException()
    {
        // Arrange
        var ruleJson = @"{
            ""id"": ""duplicate-trigger"",
            ""name"": ""Duplicate Trigger Test"",
            ""severity"": ""medium"",
            ""triggers"": [
                {
                    ""type"": ""event"",
                    ""eventType"": ""same_event""
                },
                {
                    ""type"": ""event"",
                    ""eventType"": ""same_event""
                }
            ]
        }";

        _mockValidator.Setup(v => v.ValidateRuleAsync(It.IsAny<string>()))
            .Returns(Task.FromResult(new ValidationResult(true, "Valid")));

        // Act & Assert
        await _parser.Invoking(p => p.ParseRuleAsync(ruleJson))
            .Should().ThrowAsync<RuleParsingException>()
            .WithMessage("*Duplicate event types found*");
    }
}