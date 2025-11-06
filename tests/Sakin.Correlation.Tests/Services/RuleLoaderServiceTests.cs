using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Exceptions;
using Sakin.Correlation.Models;
using Sakin.Correlation.Parsers;
using Sakin.Correlation.Services;
using Xunit;

namespace Sakin.Correlation.Tests.Services;

public class RuleLoaderServiceTests : IDisposable
{
    private readonly Mock<IRuleParser> _mockParser;
    private readonly Mock<ILogger<RuleLoaderService>> _mockLogger;
    private readonly string _tempDir;
    private RuleLoaderService? _service;

    public RuleLoaderServiceTests()
    {
        _mockParser = new Mock<IRuleParser>();
        _mockLogger = new Mock<ILogger<RuleLoaderService>>();
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _service?.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task LoadRulesAsync_WithValidRules_ShouldLoadSuccessfully()
    {
        // Arrange
        var options = CreateOptions(reloadOnChange: false);
        var testRules = CreateTestRules();

        _mockParser.Setup(p => p.ParseRulesFromDirectoryAsync(_tempDir))
            .ReturnsAsync(testRules);

        _service = new RuleLoaderService(_mockParser.Object, options, _mockLogger.Object);

        // Act
        await _service.LoadRulesAsync();

        // Assert
        _service.Rules.Should().HaveCount(2);
        _service.Rules.Should().Contain(r => r.Id == "rule1");
        _service.Rules.Should().Contain(r => r.Id == "rule2");
    }

    [Fact]
    public async Task ReloadRulesAsync_WithValidChanges_ShouldUpdateRulesAndLogDiffs()
    {
        // Arrange
        var options = CreateOptions(reloadOnChange: false);
        var initialRules = CreateTestRules();
        var updatedRules = new List<CorrelationRule>
        {
            initialRules[0], // rule1 unchanged
            new CorrelationRule
            {
                Id = "rule2",
                Name = "Rule 2 Modified",
                Severity = SeverityLevel.High,
                Triggers = new List<Trigger> { new Trigger { Type = TriggerType.Event, EventType = "event2_modified" } }
            },
            new CorrelationRule
            {
                Id = "rule3",
                Name = "Rule 3",
                Severity = SeverityLevel.Low,
                Triggers = new List<Trigger> { new Trigger { Type = TriggerType.Event, EventType = "event3" } }
            }
        };

        _mockParser.SetupSequence(p => p.ParseRulesFromDirectoryAsync(_tempDir))
            .ReturnsAsync(initialRules)
            .ReturnsAsync(updatedRules);

        _mockParser.Setup(p => p.SerializeRule(It.IsAny<CorrelationRule>()))
            .Returns<CorrelationRule>(r => System.Text.Json.JsonSerializer.Serialize(r));

        _service = new RuleLoaderService(_mockParser.Object, options, _mockLogger.Object);
        await _service.LoadRulesAsync();

        // Act
        await _service.ReloadRulesAsync();

        // Assert
        _service.Rules.Should().HaveCount(3);
        _service.Rules.Should().Contain(r => r.Id == "rule1");
        _service.Rules.Should().Contain(r => r.Id == "rule2");
        _service.Rules.Should().Contain(r => r.Id == "rule3");
        _service.Rules.First(r => r.Id == "rule2").Name.Should().Be("Rule 2 Modified");
    }

    [Fact]
    public async Task ReloadRulesAsync_WithInvalidRules_ShouldKeepPreviousValidRules()
    {
        // Arrange
        var options = CreateOptions(reloadOnChange: false);
        var initialRules = CreateTestRules();

        _mockParser.Setup(p => p.ParseRulesFromDirectoryAsync(_tempDir))
            .ReturnsAsync(initialRules);

        _service = new RuleLoaderService(_mockParser.Object, options, _mockLogger.Object);
        await _service.LoadRulesAsync();

        // Setup parser to throw on second call (invalid rules)
        _mockParser.Setup(p => p.ParseRulesFromDirectoryAsync(_tempDir))
            .ThrowsAsync(new RuleParsingException("Invalid rule"));

        // Act
        await _service.ReloadRulesAsync();

        // Assert - should still have the initial rules
        _service.Rules.Should().HaveCount(2);
        _service.Rules.Should().Contain(r => r.Id == "rule1");
        _service.Rules.Should().Contain(r => r.Id == "rule2");
    }

    [Fact]
    public async Task ReloadRulesAsync_WithRemovedRules_ShouldLogRemovedRules()
    {
        // Arrange
        var options = CreateOptions(reloadOnChange: false);
        var initialRules = CreateTestRules();
        var updatedRules = new List<CorrelationRule> { initialRules[0] }; // Only rule1

        _mockParser.SetupSequence(p => p.ParseRulesFromDirectoryAsync(_tempDir))
            .ReturnsAsync(initialRules)
            .ReturnsAsync(updatedRules);

        _mockParser.Setup(p => p.SerializeRule(It.IsAny<CorrelationRule>()))
            .Returns<CorrelationRule>(r => System.Text.Json.JsonSerializer.Serialize(r));

        _service = new RuleLoaderService(_mockParser.Object, options, _mockLogger.Object);
        await _service.LoadRulesAsync();

        // Act
        await _service.ReloadRulesAsync();

        // Assert
        _service.Rules.Should().HaveCount(1);
        _service.Rules.Should().Contain(r => r.Id == "rule1");
        _service.Rules.Should().NotContain(r => r.Id == "rule2");
    }

    [Fact]
    public async Task HotReload_WithFileChanges_ShouldReloadRules()
    {
        // Arrange
        var ruleFile = Path.Combine(_tempDir, "test-rule.json");
        var ruleJson = @"{
            ""id"": ""test-rule"",
            ""name"": ""Test Rule"",
            ""severity"": ""medium"",
            ""triggers"": [{""type"": ""event"", ""eventType"": ""test_event""}]
        }";
        await File.WriteAllTextAsync(ruleFile, ruleJson);

        var initialRules = CreateTestRules();
        var updatedRules = new List<CorrelationRule>
        {
            new CorrelationRule
            {
                Id = "test-rule",
                Name = "Test Rule",
                Severity = SeverityLevel.Medium,
                Triggers = new List<Trigger> { new Trigger { Type = TriggerType.Event, EventType = "test_event" } }
            }
        };

        _mockParser.SetupSequence(p => p.ParseRulesFromDirectoryAsync(_tempDir))
            .ReturnsAsync(initialRules)
            .ReturnsAsync(updatedRules);

        _mockParser.Setup(p => p.SerializeRule(It.IsAny<CorrelationRule>()))
            .Returns<CorrelationRule>(r => System.Text.Json.JsonSerializer.Serialize(r));

        var options = CreateOptions(reloadOnChange: true);
        _service = new RuleLoaderService(_mockParser.Object, options, _mockLogger.Object);

        // Start the service
        var cts = new CancellationTokenSource();
        var serviceTask = _service.StartAsync(cts.Token);
        await Task.Delay(100); // Allow service to initialize

        // Act - trigger file change by touching the file
        File.SetLastWriteTimeUtc(ruleFile, DateTime.UtcNow);
        
        // Wait for debounce + processing time
        await Task.Delay(500);

        // Assert
        _service.Rules.Should().HaveCount(1);
        _service.Rules.First().Id.Should().Be("test-rule");

        // Cleanup
        cts.Cancel();
        try
        {
            await serviceTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    [Fact]
    public async Task ReloadRulesAsync_WithNoChanges_ShouldLogNoChanges()
    {
        // Arrange
        var options = CreateOptions(reloadOnChange: false);
        var rules = CreateTestRules();

        _mockParser.Setup(p => p.ParseRulesFromDirectoryAsync(_tempDir))
            .ReturnsAsync(rules);

        _mockParser.Setup(p => p.SerializeRule(It.IsAny<CorrelationRule>()))
            .Returns<CorrelationRule>(r => System.Text.Json.JsonSerializer.Serialize(r));

        _service = new RuleLoaderService(_mockParser.Object, options, _mockLogger.Object);
        await _service.LoadRulesAsync();

        // Act - reload with same rules
        await _service.ReloadRulesAsync();

        // Assert - should still have same rules
        _service.Rules.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReloadRulesAsync_WithNonExistentDirectory_ShouldLogWarningAndReturn()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = Options.Create(new RulesOptions
        {
            RulesPath = nonExistentDir,
            ReloadOnChange = false
        });

        _mockParser.Setup(p => p.ParseRulesFromDirectoryAsync(nonExistentDir))
            .ThrowsAsync(new RuleParsingException("Directory not found"));

        _service = new RuleLoaderService(_mockParser.Object, options, _mockLogger.Object);

        // Act
        await _service.ReloadRulesAsync();

        // Assert - should have no rules
        _service.Rules.Should().BeEmpty();
    }

    private IOptions<RulesOptions> CreateOptions(bool reloadOnChange, int debounceMs = 300)
    {
        return Options.Create(new RulesOptions
        {
            RulesPath = _tempDir,
            ReloadOnChange = reloadOnChange,
            DebounceMilliseconds = debounceMs
        });
    }

    private List<CorrelationRule> CreateTestRules()
    {
        return new List<CorrelationRule>
        {
            new CorrelationRule
            {
                Id = "rule1",
                Name = "Rule 1",
                Severity = SeverityLevel.Medium,
                Triggers = new List<Trigger> { new Trigger { Type = TriggerType.Event, EventType = "event1" } }
            },
            new CorrelationRule
            {
                Id = "rule2",
                Name = "Rule 2",
                Severity = SeverityLevel.High,
                Triggers = new List<Trigger> { new Trigger { Type = TriggerType.Event, EventType = "event2" } }
            }
        };
    }
}
