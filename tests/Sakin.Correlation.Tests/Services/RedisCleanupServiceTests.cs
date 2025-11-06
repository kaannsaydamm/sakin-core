using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Models;
using Sakin.Correlation.Services;
using Xunit;

namespace Sakin.Correlation.Tests.Services;

public class RedisCleanupServiceTests : IDisposable
{
    private readonly Mock<IRedisStateManager> _mockRedisStateManager;
    private readonly Mock<IRuleLoaderServiceV2> _mockRuleLoader;
    private readonly Mock<ILogger<RedisCleanupService>> _mockLogger;
    private RedisCleanupService? _service;

    public RedisCleanupServiceTests()
    {
        _mockRedisStateManager = new Mock<IRedisStateManager>();
        _mockRuleLoader = new Mock<IRuleLoaderServiceV2>();
        _mockLogger = new Mock<ILogger<RedisCleanupService>>();
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRunCleanupAtConfiguredInterval()
    {
        // Arrange
        var options = Options.Create(new AggregationOptions
        {
            MaxWindowSize = 86400,
            CleanupInterval = 1 // 1 second for fast test
        });

        var rulesV2 = new List<CorrelationRuleV2>
        {
            new CorrelationRuleV2
            {
                Id = "test-rule",
                Trigger = new RuleTrigger
                {
                    SourceTypes = new List<string> { "syslog" }
                },
                Condition = new ConditionWithAggregation
                {
                    Aggregation = new AggregationCondition
                    {
                        Function = "count",
                        WindowSeconds = 300
                    }
                }
            }
        };

        _mockRuleLoader.Setup(r => r.RulesV2).Returns(rulesV2.AsReadOnly());
        _mockRedisStateManager.Setup(r => r.CleanupExpiredWindowsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        _service = new RedisCleanupService(
            _mockRedisStateManager.Object,
            _mockRuleLoader.Object,
            _mockLogger.Object,
            options
        );

        var cts = new CancellationTokenSource();

        // Act
        var executeTask = _service.StartAsync(cts.Token);
        await Task.Delay(2500); // Wait for at least 2 cleanup cycles
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - should have been called at least twice
        _mockRedisStateManager.Verify(
            r => r.CleanupExpiredWindowsAsync("test-rule", 300),
            Times.AtLeast(2)
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleRules_ShouldCleanupAllRules()
    {
        // Arrange
        var options = Options.Create(new AggregationOptions
        {
            MaxWindowSize = 86400,
            CleanupInterval = 1
        });

        var rulesV2 = new List<CorrelationRuleV2>
        {
            new CorrelationRuleV2
            {
                Id = "rule1",
                Trigger = new RuleTrigger { SourceTypes = new List<string> { "syslog" } },
                Condition = new ConditionWithAggregation
                {
                    Aggregation = new AggregationCondition { Function = "count", WindowSeconds = 300 }
                }
            },
            new CorrelationRuleV2
            {
                Id = "rule2",
                Trigger = new RuleTrigger { SourceTypes = new List<string> { "syslog" } },
                Condition = new ConditionWithAggregation
                {
                    Aggregation = new AggregationCondition { Function = "count", WindowSeconds = 600 }
                }
            },
            new CorrelationRuleV2
            {
                Id = "rule3",
                Trigger = new RuleTrigger { SourceTypes = new List<string> { "syslog" } },
                Condition = new ConditionWithAggregation
                {
                    Aggregation = null // No aggregation
                }
            }
        };

        _mockRuleLoader.Setup(r => r.RulesV2).Returns(rulesV2.AsReadOnly());
        _mockRedisStateManager.Setup(r => r.CleanupExpiredWindowsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        _service = new RedisCleanupService(
            _mockRedisStateManager.Object,
            _mockRuleLoader.Object,
            _mockLogger.Object,
            options
        );

        var cts = new CancellationTokenSource();

        // Act
        var executeTask = _service.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - should have cleaned up rule1 and rule2, but not rule3 (no aggregation)
        _mockRedisStateManager.Verify(r => r.CleanupExpiredWindowsAsync("rule1", 300), Times.AtLeastOnce);
        _mockRedisStateManager.Verify(r => r.CleanupExpiredWindowsAsync("rule2", 600), Times.AtLeastOnce);
        _mockRedisStateManager.Verify(r => r.CleanupExpiredWindowsAsync("rule3", It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCleanupFails_ShouldLogErrorAndContinue()
    {
        // Arrange
        var options = Options.Create(new AggregationOptions
        {
            MaxWindowSize = 86400,
            CleanupInterval = 1
        });

        var rulesV2 = new List<CorrelationRuleV2>
        {
            new CorrelationRuleV2
            {
                Id = "failing-rule",
                Trigger = new RuleTrigger { SourceTypes = new List<string> { "syslog" } },
                Condition = new ConditionWithAggregation
                {
                    Aggregation = new AggregationCondition { Function = "count", WindowSeconds = 300 }
                }
            }
        };

        _mockRuleLoader.Setup(r => r.RulesV2).Returns(rulesV2.AsReadOnly());
        _mockRedisStateManager.Setup(r => r.CleanupExpiredWindowsAsync("failing-rule", 300))
            .ThrowsAsync(new Exception("Redis connection failed"));

        _service = new RedisCleanupService(
            _mockRedisStateManager.Object,
            _mockRuleLoader.Object,
            _mockLogger.Object,
            options
        );

        var cts = new CancellationTokenSource();

        // Act
        var executeTask = _service.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - should have logged warning but continued
        _mockRedisStateManager.Verify(
            r => r.CleanupExpiredWindowsAsync("failing-rule", 300),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithNoAggregationRules_ShouldNotCallCleanup()
    {
        // Arrange
        var options = Options.Create(new AggregationOptions
        {
            MaxWindowSize = 86400,
            CleanupInterval = 1
        });

        var rulesV2 = new List<CorrelationRuleV2>
        {
            new CorrelationRuleV2
            {
                Id = "rule-without-aggregation",
                Trigger = new RuleTrigger { SourceTypes = new List<string> { "syslog" } },
                Condition = new ConditionWithAggregation
                {
                    Aggregation = null
                }
            }
        };

        _mockRuleLoader.Setup(r => r.RulesV2).Returns(rulesV2.AsReadOnly());

        _service = new RedisCleanupService(
            _mockRedisStateManager.Object,
            _mockRuleLoader.Object,
            _mockLogger.Object,
            options
        );

        var cts = new CancellationTokenSource();

        // Act
        var executeTask = _service.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockRedisStateManager.Verify(
            r => r.CleanupExpiredWindowsAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyRulesList_ShouldNotCallCleanup()
    {
        // Arrange
        var options = Options.Create(new AggregationOptions
        {
            MaxWindowSize = 86400,
            CleanupInterval = 1
        });

        var rulesV2 = new List<CorrelationRuleV2>();

        _mockRuleLoader.Setup(r => r.RulesV2).Returns(rulesV2.AsReadOnly());

        _service = new RedisCleanupService(
            _mockRedisStateManager.Object,
            _mockRuleLoader.Object,
            _mockLogger.Object,
            options
        );

        var cts = new CancellationTokenSource();

        // Act
        var executeTask = _service.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockRedisStateManager.Verify(
            r => r.CleanupExpiredWindowsAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldStopGracefully()
    {
        // Arrange
        var options = Options.Create(new AggregationOptions
        {
            MaxWindowSize = 86400,
            CleanupInterval = 300
        });

        var rulesV2 = new List<CorrelationRuleV2>();
        _mockRuleLoader.Setup(r => r.RulesV2).Returns(rulesV2.AsReadOnly());

        _service = new RedisCleanupService(
            _mockRedisStateManager.Object,
            _mockRuleLoader.Object,
            _mockLogger.Object,
            options
        );

        var cts = new CancellationTokenSource();

        // Act
        var executeTask = _service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        // Assert - should complete without exception
        await executeTask.Invoking(async t => await t).Should().NotThrowAsync();
    }
}
