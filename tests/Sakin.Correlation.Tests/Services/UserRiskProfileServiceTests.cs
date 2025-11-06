using Microsoft.Extensions.Logging;
using Moq;
using Sakin.Common.Cache;
using Sakin.Common.Models;
using Sakin.Correlation.Services;
using Xunit;

namespace Sakin.Correlation.Tests.Services;

public class UserRiskProfileServiceTests
{
    private readonly Mock<IRedisClient> _mockRedisClient;
    private readonly Mock<ILogger<UserRiskProfileService>> _mockLogger;
    private readonly UserRiskProfileService _service;

    public UserRiskProfileServiceTests()
    {
        _mockRedisClient = new Mock<IRedisClient>();
        _mockLogger = new Mock<ILogger<UserRiskProfileService>>();
        _service = new UserRiskProfileService(_mockRedisClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetUserRiskScore_WithExistingUser_ShouldReturnScore()
    {
        // Arrange
        var username = "testuser";
        var riskData = new Dictionary<string, object>
        {
            ["score"] = 25.5,
            ["last_updated"] = DateTimeOffset.UtcNow
        };

        _mockRedisClient
            .Setup(x => x.GetAsync<Dictionary<string, object>>("sakin:user_risk:testuser"))
            .ReturnsAsync(riskData);

        // Act
        var result = await _service.GetUserRiskScore(username);

        // Assert
        Assert.Equal(25.5, result);
    }

    [Fact]
    public async Task GetUserRiskScore_WithNonExistentUser_ShouldReturnZero()
    {
        // Arrange
        var username = "nonexistentuser";

        _mockRedisClient
            .Setup(x => x.GetAsync<Dictionary<string, object>>("sakin:user_risk:nonexistentuser"))
            .ReturnsAsync((Dictionary<string, object>?)null);

        // Act
        var result = await _service.GetUserRiskScore(username);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetUserRiskScore_WithEmptyUsername_ShouldReturnZero()
    {
        // Arrange
        string username = "";

        // Act
        var result = await _service.GetUserRiskScore(username);

        // Assert
        Assert.Equal(0, result);
        _mockRedisClient.Verify(x => x.GetAsync<Dictionary<string, object>>(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetUserRiskScore_WithScoreAboveMax_ShouldCapAtMax()
    {
        // Arrange
        var username = "highriskuser";
        var riskData = new Dictionary<string, object>
        {
            ["score"] = 75.0, // Above max of 50
            ["last_updated"] = DateTimeOffset.UtcNow
        };

        _mockRedisClient
            .Setup(x => x.GetAsync<Dictionary<string, object>>("sakin:user_risk:highriskuser"))
            .ReturnsAsync(riskData);

        // Act
        var result = await _service.GetUserRiskScore(username);

        // Assert
        Assert.Equal(50, result); // Should be capped at max
    }

    [Fact]
    public async Task UpdateUserRiskProfileAsync_WithFailedLoginEvent_ShouldIncreaseScore()
    {
        // Arrange
        var normalizedEvent = new NormalizedEvent
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            EventType = "auth.failed",
            Timestamp = DateTimeOffset.UtcNow
        };

        _mockRedisClient
            .Setup(x => x.GetAsync<Dictionary<string, object>>("sakin:user_risk:testuser"))
            .ReturnsAsync(new Dictionary<string, object>());

        // Act
        await _service.UpdateUserRiskProfileAsync(normalizedEvent);

        // Assert
        _mockRedisClient.Verify(
            x => x.SetAsync(
                "sakin:user_risk:testuser",
                It.Is<Dictionary<string, object>>(d => 
                    (double)d["score"] == 5.0 && // Should increase by 5 for failed login
                    d.ContainsKey("last_updated") &&
                    d["last_event_type"].ToString() == "auth.failed"),
                TimeSpan.FromDays(7)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateUserRiskProfileAsync_WithPrivilegeEscalationEvent_ShouldIncreaseScoreSignificantly()
    {
        // Arrange
        var normalizedEvent = new NormalizedEvent
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            EventType = "auth.privilege_escalation",
            Timestamp = DateTimeOffset.UtcNow
        };

        _mockRedisClient
            .Setup(x => x.GetAsync<Dictionary<string, object>>("sakin:user_risk:testuser"))
            .ReturnsAsync(new Dictionary<string, object>());

        // Act
        await _service.UpdateUserRiskProfileAsync(normalizedEvent);

        // Assert
        _mockRedisClient.Verify(
            x => x.SetAsync(
                "sakin:user_risk:testuser",
                It.Is<Dictionary<string, object>>(d => (double)d["score"] == 15.0), // Should increase by 15
                TimeSpan.FromDays(7)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateUserRiskProfileAsync_WithExistingScore_ShouldAddToExisting()
    {
        // Arrange
        var normalizedEvent = new NormalizedEvent
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            EventType = "auth.failed",
            Timestamp = DateTimeOffset.UtcNow
        };

        var existingRiskData = new Dictionary<string, object>
        {
            ["score"] = 20.0,
            ["last_updated"] = DateTimeOffset.UtcNow.AddDays(-1)
        };

        _mockRedisClient
            .Setup(x => x.GetAsync<Dictionary<string, object>>("sakin:user_risk:testuser"))
            .ReturnsAsync(existingRiskData);

        // Act
        await _service.UpdateUserRiskProfileAsync(normalizedEvent);

        // Assert
        _mockRedisClient.Verify(
            x => x.SetAsync(
                "sakin:user_risk:testuser",
                It.Is<Dictionary<string, object>>(d => (double)d["score"] == 25.0), // 20 + 5
                TimeSpan.FromDays(7)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateUserRiskProfileAsync_WithScoreAboveMax_ShouldCapAtMax()
    {
        // Arrange
        var normalizedEvent = new NormalizedEvent
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            EventType = "malware.detected", // +25 points
            Timestamp = DateTimeOffset.UtcNow
        };

        var existingRiskData = new Dictionary<string, object>
        {
            ["score"] = 40.0 // Already high
        };

        _mockRedisClient
            .Setup(x => x.GetAsync<Dictionary<string, object>>("sakin:user_risk:testuser"))
            .ReturnsAsync(existingRiskData);

        // Act
        await _service.UpdateUserRiskProfileAsync(normalizedEvent);

        // Assert
        _mockRedisClient.Verify(
            x => x.SetAsync(
                "sakin:user_risk:testuser",
                It.Is<Dictionary<string, object>>(d => (double)d["score"] == 50.0), // Capped at 50
                TimeSpan.FromDays(7)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateUserRiskProfileAsync_WithNullUsername_ShouldReturnEarly()
    {
        // Arrange
        var normalizedEvent = new NormalizedEvent
        {
            Id = Guid.NewGuid(),
            Username = null,
            EventType = "auth.failed",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        await _service.UpdateUserRiskProfileAsync(normalizedEvent);

        // Assert
        _mockRedisClient.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()),
            Times.Never);
    }

    [Theory]
    [InlineData("auth.failed", 5.0)]
    [InlineData("auth.lockout", 10.0)]
    [InlineData("auth.privilege_escalation", 15.0)]
    [InlineData("auth.unusual_time", 8.0)]
    [InlineData("auth.unusual_location", 12.0)]
    [InlineData("data.access_sensitive", 7.0)]
    [InlineData("data.exfiltration_attempt", 20.0)]
    [InlineData("malware.detected", 25.0)]
    [InlineData("policy.violation", 10.0)]
    [InlineData("network.suspicious_traffic", 8.0)]
    [InlineData("network.command_and_control", 30.0)]
    [InlineData("random.event", 1.0)] // Default case
    public async Task UpdateUserRiskProfileAsync_WithDifferentEventTypes_ShouldApplyCorrectRiskIncrease(
        string eventType, double expectedIncrease)
    {
        // Arrange
        var normalizedEvent = new NormalizedEvent
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            EventType = eventType,
            Timestamp = DateTimeOffset.UtcNow
        };

        _mockRedisClient
            .Setup(x => x.GetAsync<Dictionary<string, object>>("sakin:user_risk:testuser"))
            .ReturnsAsync(new Dictionary<string, object>());

        // Act
        await _service.UpdateUserRiskProfileAsync(normalizedEvent);

        // Assert
        _mockRedisClient.Verify(
            x => x.SetAsync(
                "sakin:user_risk:testuser",
                It.Is<Dictionary<string, object>>(d => (double)d["score"] == expectedIncrease),
                TimeSpan.FromDays(7)),
            Times.Once);
    }
}