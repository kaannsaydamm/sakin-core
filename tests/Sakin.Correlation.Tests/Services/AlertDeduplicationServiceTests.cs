using Microsoft.Extensions.Logging;
using Moq;
using Sakin.Common.Cache;
using Sakin.Common.Models;
using Sakin.Correlation.Services;
using Xunit;

namespace Sakin.Correlation.Tests.Services;

public class AlertDeduplicationServiceTests
{
    private readonly Mock<IRedisClient> _mockRedisClient;
    private readonly Mock<ILogger<AlertDeduplicationService>> _mockLogger;
    private readonly AlertDeduplicationService _service;

    public AlertDeduplicationServiceTests()
    {
        _mockRedisClient = new Mock<IRedisClient>();
        _mockLogger = new Mock<ILogger<AlertDeduplicationService>>();
        _service = new AlertDeduplicationService(_mockRedisClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GenerateDedupKeyAsync_WithValidInput_ReturnsConsistentHash()
    {
        // Arrange
        var ruleId = "rule-123";
        var eventEnvelope = new EventEnvelope
        {
            Source = "192.168.1.100",
            Normalized = new Dictionary<string, object?> { { "destination_ip", "10.0.0.1" } }
        };

        // Act
        var key1 = await _service.GenerateDedupKeyAsync(ruleId, eventEnvelope);
        var key2 = await _service.GenerateDedupKeyAsync(ruleId, eventEnvelope);

        // Assert
        Assert.Equal(key1, key2);
        Assert.StartsWith("dedup:", key1);
    }

    [Fact]
    public async Task IsAlertDuplicateAsync_WhenKeyExistsInRedis_ReturnsTrue()
    {
        // Arrange
        var dedupKey = "dedup:abc123";
        var ttl = TimeSpan.FromHours(1);
        _mockRedisClient
            .Setup(r => r.GetStringAsync(dedupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync("existing-alert-id");

        // Act
        var isDuplicate = await _service.IsAlertDuplicateAsync(dedupKey, ttl);

        // Assert
        Assert.True(isDuplicate);
    }

    [Fact]
    public async Task IsAlertDuplicateAsync_WhenKeyNotInRedis_ReturnsFalse()
    {
        // Arrange
        var dedupKey = "dedup:abc123";
        var ttl = TimeSpan.FromHours(1);
        _mockRedisClient
            .Setup(r => r.GetStringAsync(dedupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var isDuplicate = await _service.IsAlertDuplicateAsync(dedupKey, ttl);

        // Assert
        Assert.False(isDuplicate);
    }

    [Fact]
    public async Task StoreDeduplicationKeyAsync_StoresKeyInRedis()
    {
        // Arrange
        var dedupKey = "dedup:abc123";
        var alertId = Guid.NewGuid();
        var ttl = TimeSpan.FromHours(1);

        // Act
        await _service.StoreDeduplicationKeyAsync(dedupKey, alertId, ttl);

        // Assert
        _mockRedisClient.Verify(
            r => r.SetStringAsync(dedupKey, alertId.ToString(), ttl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDuplicateAlertIdAsync_WithValidKey_ReturnsGuid()
    {
        // Arrange
        var dedupKey = "dedup:abc123";
        var expectedAlertId = Guid.NewGuid();
        _mockRedisClient
            .Setup(r => r.GetStringAsync(dedupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAlertId.ToString());

        // Act
        var result = await _service.GetDuplicateAlertIdAsync(dedupKey);

        // Assert
        Assert.Equal(expectedAlertId, result);
    }

    [Fact]
    public async Task GetDuplicateAlertIdAsync_WithMissingKey_ReturnsNull()
    {
        // Arrange
        var dedupKey = "dedup:abc123";
        _mockRedisClient
            .Setup(r => r.GetStringAsync(dedupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.GetDuplicateAlertIdAsync(dedupKey);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateDedupKeyAsync_DifferentRules_DifferentKeys()
    {
        // Arrange
        var eventEnvelope = new EventEnvelope
        {
            Source = "192.168.1.100",
            Normalized = new Dictionary<string, object?> { { "destination_ip", "10.0.0.1" } }
        };

        // Act
        var key1 = await _service.GenerateDedupKeyAsync("rule-1", eventEnvelope);
        var key2 = await _service.GenerateDedupKeyAsync("rule-2", eventEnvelope);

        // Assert
        Assert.NotEqual(key1, key2);
    }
}
