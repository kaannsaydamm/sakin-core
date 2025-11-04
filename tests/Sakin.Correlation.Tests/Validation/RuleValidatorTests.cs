using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sakin.Correlation.Exceptions;
using Sakin.Correlation.Models;
using Sakin.Correlation.Validation;
using System.Text.Json;
using Xunit;

namespace Sakin.Correlation.Tests.Validation;

public class RuleValidatorTests
{
    private readonly Mock<ILogger<RuleValidator>> _mockLogger;
    private readonly RuleValidator _validator;

    public RuleValidatorTests()
    {
        _mockLogger = new Mock<ILogger<RuleValidator>>();
        _validator = new RuleValidator(_mockLogger.Object);
    }

    [Fact]
    public async Task ValidateRuleAsync_WithValidRule_ShouldReturnSuccess()
    {
        // Arrange
        var ruleJson = @"{
            ""id"": ""valid-rule"",
            ""name"": ""Valid Rule"",
            ""severity"": ""medium"",
            ""triggers"": [
                {
                    ""type"": ""event"",
                    ""eventType"": ""test_event""
                }
            ]
        }";

        // Act
        var result = await _validator.ValidateRuleAsync(ruleJson);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Message.Should().Be("Rule is valid");
    }

    [Fact]
    public async Task ValidateRuleAsync_WithInvalidJson_ShouldReturnFailure()
    {
        // Arrange
        var invalidJson = @"{ invalid json }";

        // Act
        var result = await _validator.ValidateRuleAsync(invalidJson);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("JSON parsing error");
    }

    [Fact]
    public async Task ValidateRuleAsync_WithMissingRequiredFields_ShouldReturnFailure()
    {
        // Arrange
        var ruleJson = @"{
            ""name"": ""Missing ID Rule""
        }";

        // Act
        var result = await _validator.ValidateRuleAsync(ruleJson);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("Rule ID is required");
    }

    [Fact]
    public async Task ValidateRuleAsync_WithInvalidRuleId_ShouldReturnFailure()
    {
        // Arrange
        var ruleJson = @"{
            ""id"": ""invalid id with spaces"",
            ""name"": ""Invalid ID Rule"",
            ""severity"": ""medium"",
            ""triggers"": [
                {
                    ""type"": ""event"",
                    ""eventType"": ""test_event""
                }
            ]
        }";

        // Act
        var result = await _validator.ValidateRuleAsync(ruleJson);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("Rule ID can only contain alphanumeric characters");
    }

    [Fact]
    public async Task ValidateRuleAsync_WithNoTriggers_ShouldReturnFailure()
    {
        // Arrange
        var ruleJson = @"{
            ""id"": ""no-triggers"",
            ""name"": ""No Triggers Rule"",
            ""severity"": ""medium""
        }";

        // Act
        var result = await _validator.ValidateRuleAsync(ruleJson);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("At least one trigger is required");
    }

    [Fact]
    public async Task ValidateRuleAsync_WithInvalidRegexPattern_ShouldReturnFailure()
    {
        // Arrange
        var ruleJson = @"{
            ""id"": ""invalid-regex"",
            ""name"": ""Invalid Regex Rule"",
            ""severity"": ""medium"",
            ""triggers"": [
                {
                    ""type"": ""event"",
                    ""eventType"": ""test_event""
                }
            ],
            ""conditions"": [
                {
                    ""field"": ""test_field"",
                    ""operator"": ""regex"",
                    ""value"": ""[invalid regex""
                }
            ]
        }";

        // Act
        var result = await _validator.ValidateRuleAsync(ruleJson);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("Invalid regex pattern");
    }

    [Fact]
    public async Task ValidateRuleAsync_WithInvalidAggregationSize_ShouldReturnFailure()
    {
        // Arrange
        var ruleJson = @"{
            ""id"": ""invalid-aggregation"",
            ""name"": ""Invalid Aggregation Rule"",
            ""severity"": ""medium"",
            ""triggers"": [
                {
                    ""type"": ""event"",
                    ""eventType"": ""test_event""
                }
            ],
            ""aggregation"": {
                ""type"": ""time_window"",
                ""size"": -1,
                ""unit"": ""minutes""
            }
        }";

        // Act
        var result = await _validator.ValidateRuleAsync(ruleJson);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("Aggregation size must be greater than 0");
    }

    [Fact]
    public async Task ValidateRuleAsync_WithWebhookActionWithoutUrl_ShouldReturnFailure()
    {
        // Arrange
        var ruleJson = @"{
            ""id"": ""webhook-no-url"",
            ""name"": ""Webhook No URL Rule"",
            ""severity"": ""medium"",
            ""triggers"": [
                {
                    ""type"": ""event"",
                    ""eventType"": ""test_event""
                }
            ],
            ""actions"": [
                {
                    ""type"": ""webhook"",
                    ""parameters"": {
                        ""method"": ""POST""
                    }
                }
            ]
        }";

        // Act
        var result = await _validator.ValidateRuleAsync(ruleJson);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("Webhook actions require a 'url' parameter");
    }

    [Fact]
    public async Task ValidateRuleAsync_WithEmailActionWithoutRecipients_ShouldReturnFailure()
    {
        // Arrange
        var ruleJson = @"{
            ""id"": ""email-no-recipients"",
            ""name"": ""Email No Recipients Rule"",
            ""severity"": ""medium"",
            ""triggers"": [
                {
                    ""type"": ""event"",
                    ""eventType"": ""test_event""
                }
            ],
            ""actions"": [
                {
                    ""type"": ""email"",
                    ""parameters"": {
                        ""subject"": ""Test Email""
                    }
                }
            ]
        }";

        // Act
        var result = await _validator.ValidateRuleAsync(ruleJson);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("Email actions require a 'recipients' parameter");
    }

    [Fact]
    public async Task ValidateRuleAsync_WithInvalidRetryPolicy_ShouldReturnFailure()
    {
        // Arrange
        var ruleJson = @"{
            ""id"": ""invalid-retry"",
            ""name"": ""Invalid Retry Rule"",
            ""severity"": ""medium"",
            ""triggers"": [
                {
                    ""type"": ""event"",
                    ""eventType"": ""test_event""
                }
            ],
            ""actions"": [
                {
                    ""type"": ""webhook"",
                    ""parameters"": {
                        ""url"": ""https://example.com/webhook""
                    },
                    ""retry"": {
                        ""attempts"": -1,
                        ""delay"": -1000
                    }
                }
            ]
        }";

        // Act
        var result = await _validator.ValidateRuleAsync(ruleJson);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("Retry attempts must be greater than 0");
        result.Message.Should().Contain("Retry delay cannot be negative");
    }

    [Fact]
    public async Task ValidateRuleAsync_WithCompleteValidRule_ShouldReturnSuccess()
    {
        // Arrange
        var ruleJson = @"{
            ""id"": ""complete-valid-rule"",
            ""name"": ""Complete Valid Rule"",
            ""description"": ""A complete valid rule for testing"",
            ""enabled"": true,
            ""severity"": ""high"",
            ""triggers"": [
                {
                    ""type"": ""event"",
                    ""eventType"": ""security_event"",
                    ""source"": ""firewall"",
                    ""filters"": {
                        ""severity"": ""high""
                    }
                }
            ],
            ""conditions"": [
                {
                    ""field"": ""source_ip"",
                    ""operator"": ""exists""
                },
                {
                    ""field"": ""threat_score"",
                    ""operator"": ""greater_than"",
                    ""value"": 7
                }
            ],
            ""aggregation"": {
                ""type"": ""time_window"",
                ""size"": 5,
                ""unit"": ""minutes"",
                ""groupBy"": [""source_ip""],
                ""having"": {
                    ""field"": ""count"",
                    ""operator"": ""greater_than"",
                    ""value"": 10
                }
            },
            ""actions"": [
                {
                    ""type"": ""alert"",
                    ""parameters"": {
                        ""title"": ""Security Alert"",
                        ""message"": ""High threat detected""
                    }
                },
                {
                    ""type"": ""webhook"",
                    ""parameters"": {
                        ""url"": ""https://api.company.com/alerts""
                    },
                    ""retry"": {
                        ""attempts"": 3,
                        ""delay"": 1000,
                        ""backoff"": ""exponential""
                    }
                }
            ],
            ""metadata"": {
                ""category"": ""security"",
                ""author"": ""security-team""
            }
        }";

        // Act
        var result = await _validator.ValidateRuleAsync(ruleJson);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Message.Should().Be("Rule is valid");
    }

    [Fact]
    public void ValidateRuleSyntax_WithValidJson_ShouldReturnSuccess()
    {
        // Arrange
        var validJson = @"{
            ""id"": ""syntax-test"",
            ""name"": ""Syntax Test"",
            ""severity"": ""medium"",
            ""triggers"": [
                {
                    ""type"": ""event"",
                    ""eventType"": ""test_event""
                }
            ]
        }";

        // Act
        var result = _validator.ValidateRuleSyntax(validJson);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Message.Should().Contain("valid");
    }

    [Fact]
    public void ValidateRuleSyntax_WithInvalidJson_ShouldReturnFailure()
    {
        // Arrange
        var invalidJson = @"{ invalid json structure }";

        // Act
        var result = _validator.ValidateRuleSyntax(invalidJson);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("Invalid JSON");
    }

    [Fact]
    public void ValidateRuleSyntax_WithSchemaViolation_ShouldReturnFailure()
    {
        // Arrange
        var schemaInvalidJson = @"{
            ""id"": ""schema-test"",
            ""name"": ""Schema Test"",
            ""severity"": ""invalid_severity"",
            ""triggers"": []
        }";

        // Act
        var result = _validator.ValidateRuleSyntax(schemaInvalidJson);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("At");
    }
}