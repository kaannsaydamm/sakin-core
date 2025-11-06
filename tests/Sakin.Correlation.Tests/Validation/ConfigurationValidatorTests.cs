using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Sakin.Correlation.Validation;
using Xunit;

namespace Sakin.Correlation.Tests.Validation;

public class ConfigurationValidatorTests
{
    private readonly Mock<ILogger<ConfigurationValidator>> _mockLogger;
    private readonly ConfigurationValidator _validator;

    public ConfigurationValidatorTests()
    {
        _mockLogger = new Mock<ILogger<ConfigurationValidator>>();
        _validator = new ConfigurationValidator(_mockLogger.Object);
    }

    [Fact]
    public void ValidateConfiguration_WithValidConfiguration_ShouldPass()
    {
        // Arrange
        var configuration = CreateValidConfiguration();

        // Act & Assert
        _validator.Invoking(v => v.ValidateConfiguration(configuration))
            .Should().NotThrow();
    }

    [Fact]
    public void ValidateConfiguration_WithMissingRulesSection_ShouldThrow()
    {
        // Arrange
        var configData = new Dictionary<string, string>
        {
            ["Redis:ConnectionString"] = "localhost:6379",
            ["Redis:KeyPrefix"] = "sakin:",
            ["Redis:DefaultTTL"] = "3600",
            ["Aggregation:MaxWindowSize"] = "86400",
            ["Aggregation:CleanupInterval"] = "300",
            ["Kafka:BootstrapServers"] = "localhost:9092",
            ["Kafka:Topic"] = "events",
            ["Kafka:ConsumerGroup"] = "correlation"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        _validator.Invoking(v => v.ValidateConfiguration(configuration))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Rules configuration section is missing*");
    }

    [Fact]
    public void ValidateConfiguration_WithEmptyRulesPath_ShouldThrow()
    {
        // Arrange
        var configData = new Dictionary<string, string>
        {
            ["Rules:RulesPath"] = "",
            ["Rules:ReloadOnChange"] = "false",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["Redis:KeyPrefix"] = "sakin:",
            ["Redis:DefaultTTL"] = "3600",
            ["Aggregation:MaxWindowSize"] = "86400",
            ["Aggregation:CleanupInterval"] = "300",
            ["Kafka:BootstrapServers"] = "localhost:9092",
            ["Kafka:Topic"] = "events",
            ["Kafka:ConsumerGroup"] = "correlation"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        _validator.Invoking(v => v.ValidateConfiguration(configuration))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Rules.RulesPath is required*");
    }

    [Fact]
    public void ValidateConfiguration_WithNegativeDebounceMilliseconds_ShouldThrow()
    {
        // Arrange
        var configData = new Dictionary<string, string>
        {
            ["Rules:RulesPath"] = "/configs/rules",
            ["Rules:ReloadOnChange"] = "true",
            ["Rules:DebounceMilliseconds"] = "-100",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["Redis:KeyPrefix"] = "sakin:",
            ["Redis:DefaultTTL"] = "3600",
            ["Aggregation:MaxWindowSize"] = "86400",
            ["Aggregation:CleanupInterval"] = "300",
            ["Kafka:BootstrapServers"] = "localhost:9092",
            ["Kafka:Topic"] = "events",
            ["Kafka:ConsumerGroup"] = "correlation"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        _validator.Invoking(v => v.ValidateConfiguration(configuration))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Rules.DebounceMilliseconds must be non-negative*");
    }

    [Fact]
    public void ValidateConfiguration_WithMissingRedisConnectionString_ShouldThrow()
    {
        // Arrange
        var configData = new Dictionary<string, string>
        {
            ["Rules:RulesPath"] = "/configs/rules",
            ["Redis:ConnectionString"] = "",
            ["Redis:KeyPrefix"] = "sakin:",
            ["Redis:DefaultTTL"] = "3600",
            ["Aggregation:MaxWindowSize"] = "86400",
            ["Aggregation:CleanupInterval"] = "300",
            ["Kafka:BootstrapServers"] = "localhost:9092",
            ["Kafka:Topic"] = "events",
            ["Kafka:ConsumerGroup"] = "correlation"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        _validator.Invoking(v => v.ValidateConfiguration(configuration))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Redis.ConnectionString is required*");
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidRedisTTL_ShouldThrow()
    {
        // Arrange
        var configData = new Dictionary<string, string>
        {
            ["Rules:RulesPath"] = "/configs/rules",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["Redis:KeyPrefix"] = "sakin:",
            ["Redis:DefaultTTL"] = "0",
            ["Aggregation:MaxWindowSize"] = "86400",
            ["Aggregation:CleanupInterval"] = "300",
            ["Kafka:BootstrapServers"] = "localhost:9092",
            ["Kafka:Topic"] = "events",
            ["Kafka:ConsumerGroup"] = "correlation"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        _validator.Invoking(v => v.ValidateConfiguration(configuration))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Redis.DefaultTTL must be positive*");
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidAggregationMaxWindowSize_ShouldThrow()
    {
        // Arrange
        var configData = new Dictionary<string, string>
        {
            ["Rules:RulesPath"] = "/configs/rules",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["Redis:KeyPrefix"] = "sakin:",
            ["Redis:DefaultTTL"] = "3600",
            ["Aggregation:MaxWindowSize"] = "0",
            ["Aggregation:CleanupInterval"] = "300",
            ["Kafka:BootstrapServers"] = "localhost:9092",
            ["Kafka:Topic"] = "events",
            ["Kafka:ConsumerGroup"] = "correlation"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        _validator.Invoking(v => v.ValidateConfiguration(configuration))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Aggregation.MaxWindowSize must be positive*");
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidCleanupInterval_ShouldThrow()
    {
        // Arrange
        var configData = new Dictionary<string, string>
        {
            ["Rules:RulesPath"] = "/configs/rules",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["Redis:KeyPrefix"] = "sakin:",
            ["Redis:DefaultTTL"] = "3600",
            ["Aggregation:MaxWindowSize"] = "86400",
            ["Aggregation:CleanupInterval"] = "-1",
            ["Kafka:BootstrapServers"] = "localhost:9092",
            ["Kafka:Topic"] = "events",
            ["Kafka:ConsumerGroup"] = "correlation"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        _validator.Invoking(v => v.ValidateConfiguration(configuration))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Aggregation.CleanupInterval must be positive*");
    }

    [Fact]
    public void ValidateConfiguration_WithMissingKafkaConfiguration_ShouldThrow()
    {
        // Arrange
        var configData = new Dictionary<string, string>
        {
            ["Rules:RulesPath"] = "/configs/rules",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["Redis:KeyPrefix"] = "sakin:",
            ["Redis:DefaultTTL"] = "3600",
            ["Aggregation:MaxWindowSize"] = "86400",
            ["Aggregation:CleanupInterval"] = "300"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        _validator.Invoking(v => v.ValidateConfiguration(configuration))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Kafka configuration section is missing*");
    }

    [Fact]
    public void ValidateConfiguration_WithMissingKafkaTopic_ShouldThrow()
    {
        // Arrange
        var configData = new Dictionary<string, string>
        {
            ["Rules:RulesPath"] = "/configs/rules",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["Redis:KeyPrefix"] = "sakin:",
            ["Redis:DefaultTTL"] = "3600",
            ["Aggregation:MaxWindowSize"] = "86400",
            ["Aggregation:CleanupInterval"] = "300",
            ["Kafka:BootstrapServers"] = "localhost:9092",
            ["Kafka:Topic"] = "",
            ["Kafka:ConsumerGroup"] = "correlation"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        _validator.Invoking(v => v.ValidateConfiguration(configuration))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Kafka.Topic is required*");
    }

    [Fact]
    public void ValidateConfiguration_WithMultipleErrors_ShouldThrowWithAllErrors()
    {
        // Arrange
        var configData = new Dictionary<string, string>
        {
            ["Rules:RulesPath"] = "",
            ["Redis:ConnectionString"] = "",
            ["Redis:KeyPrefix"] = "sakin:",
            ["Redis:DefaultTTL"] = "0",
            ["Aggregation:MaxWindowSize"] = "0",
            ["Aggregation:CleanupInterval"] = "0",
            ["Kafka:BootstrapServers"] = "",
            ["Kafka:Topic"] = "",
            ["Kafka:ConsumerGroup"] = ""
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        var exception = _validator.Invoking(v => v.ValidateConfiguration(configuration))
            .Should().Throw<InvalidOperationException>()
            .Which;

        exception.Message.Should().Contain("Rules.RulesPath is required");
        exception.Message.Should().Contain("Redis.ConnectionString is required");
        exception.Message.Should().Contain("Redis.DefaultTTL must be positive");
        exception.Message.Should().Contain("Aggregation.MaxWindowSize must be positive");
        exception.Message.Should().Contain("Aggregation.CleanupInterval must be positive");
        exception.Message.Should().Contain("Kafka.BootstrapServers is required");
        exception.Message.Should().Contain("Kafka.Topic is required");
        exception.Message.Should().Contain("Kafka.ConsumerGroup is required");
    }

    private IConfiguration CreateValidConfiguration()
    {
        var configData = new Dictionary<string, string>
        {
            ["Rules:RulesPath"] = "/configs/rules",
            ["Rules:ReloadOnChange"] = "true",
            ["Rules:DebounceMilliseconds"] = "300",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["Redis:KeyPrefix"] = "sakin:correlation:",
            ["Redis:DefaultTTL"] = "3600",
            ["Aggregation:MaxWindowSize"] = "86400",
            ["Aggregation:CleanupInterval"] = "300",
            ["Kafka:BootstrapServers"] = "localhost:9092",
            ["Kafka:Topic"] = "normalized-events",
            ["Kafka:ConsumerGroup"] = "correlation-engine"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();
    }
}
