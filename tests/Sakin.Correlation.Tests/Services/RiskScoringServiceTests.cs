using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Sakin.Common.Models;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Entities;
using Sakin.Correlation.Services;
using Xunit;

namespace Sakin.Correlation.Tests.Services;

public class RiskScoringServiceTests
{
    private readonly Mock<ITimeOfDayService> _mockTimeService;
    private readonly Mock<IUserRiskProfileService> _mockUserRiskService;
    private readonly Mock<ILogger<RiskScoringService>> _mockLogger;
    private readonly RiskScoringConfiguration _config;
    private readonly RiskScoringService _service;

    public RiskScoringServiceTests()
    {
        _mockTimeService = new Mock<ITimeOfDayService>();
        _mockUserRiskService = new Mock<IUserRiskProfileService>();
        _mockLogger = new Mock<ILogger<RiskScoringService>>();

        _config = new RiskScoringConfiguration
        {
            Enabled = true,
            Factors = new RiskScoringFactorsConfiguration
            {
                BaseWeights = new Dictionary<string, int>
                {
                    ["Low"] = 20,
                    ["Medium"] = 50,
                    ["High"] = 75,
                    ["Critical"] = 100
                },
                AssetMultipliers = new Dictionary<string, double>
                {
                    ["Low"] = 1.0,
                    ["Medium"] = 1.2,
                    ["High"] = 1.5,
                    ["Critical"] = 2.0
                },
                OffHoursMultiplier = 1.2,
                ThreatIntelMaxBoost = 30,
                AnomalyMaxBoost = 20
            },
            BusinessHours = "09:00-17:00"
        };

        var configOptions = Options.Create(_config);
        _service = new RiskScoringService(_mockTimeService.Object, _mockUserRiskService.Object, _mockLogger.Object, configOptions);
    }

    [Fact]
    public async Task CalculateRisk_WithLowSeverityAndLowRiskAsset_ShouldReturnLowScore()
    {
        // Arrange
        var alert = CreateAlert(SeverityLevel.Low);
        var envelope = CreateEventEnvelope(assetCriticality: AssetCriticality.Low);

        _mockTimeService.Setup(x => x.IsOffHours(It.IsAny<DateTimeOffset>())).Returns(false);
        _mockUserRiskService.Setup(x => x.GetUserRiskScore(It.IsAny<string>())).ReturnsAsync(0);

        // Act
        var result = await _service.CalculateRiskAsync(alert, envelope);

        // Assert
        Assert.Equal(20, result.Score);
        Assert.Equal(RiskLevel.Low, result.Level);
        Assert.Equal(20, result.Factors["base_severity"]);
        Assert.Equal(0, result.Factors["asset_boost"]);
        Assert.Equal(1.0, result.Factors["time_of_day_multiplier"]);
    }

    [Fact]
    public async Task CalculateRisk_WithHighSeverityAndCriticalAsset_ShouldReturnHighScore()
    {
        // Arrange
        var alert = CreateAlert(SeverityLevel.High);
        var envelope = CreateEventEnvelope(assetCriticality: AssetCriticality.Critical);

        _mockTimeService.Setup(x => x.IsOffHours(It.IsAny<DateTimeOffset>())).Returns(false);
        _mockUserRiskService.Setup(x => x.GetUserRiskScore(It.IsAny<string>())).ReturnsAsync(0);

        // Act
        var result = await _service.CalculateRiskAsync(alert, envelope);

        // Assert
        Assert.Equal(150, result.Score); // 75 * 2.0 = 150, but capped at 100
        Assert.Equal(RiskLevel.Critical, result.Level);
        Assert.Equal(75, result.Factors["base_severity"]);
        Assert.Equal(75, result.Factors["asset_boost"]); // Additional points from critical asset
    }

    [Fact]
    public async Task CalculateRisk_WithThreatIntelAndOffHours_ShouldApplyBoosts()
    {
        // Arrange
        var alert = CreateAlert(SeverityLevel.Medium);
        var envelope = CreateEventEnvelope(
            assetCriticality: AssetCriticality.Medium,
            threatIntelScore: new ThreatIntelScore { IsKnownMalicious = true, Score = 90 });

        _mockTimeService.Setup(x => x.IsOffHours(It.IsAny<DateTimeOffset>())).Returns(true);
        _mockUserRiskService.Setup(x => x.GetUserRiskScore(It.IsAny<string>())).ReturnsAsync(0);

        // Act
        var result = await _service.CalculateRiskAsync(alert, envelope);

        // Assert
        Assert.True(result.Score > 50); // Should be boosted by threat intel and off-hours
        Assert.Equal(RiskLevel.High, result.Level);
        Assert.True(result.Factors.ContainsKey("threat_intel_boost"));
        Assert.True(result.Factors.ContainsKey("time_of_day_multiplier"));
        Assert.Equal(1.2, result.Factors["time_of_day_multiplier"]);
    }

    [Fact]
    public async Task CalculateRisk_WithHighRiskUser_ShouldApplyUserBoost()
    {
        // Arrange
        var alert = CreateAlert(SeverityLevel.Medium);
        var envelope = CreateEventEnvelope(username: "highriskuser");

        _mockTimeService.Setup(x => x.IsOffHours(It.IsAny<DateTimeOffset>())).Returns(false);
        _mockUserRiskService.Setup(x => x.GetUserRiskScore("highriskuser")).ReturnsAsync(25);

        // Act
        var result = await _service.CalculateRiskAsync(alert, envelope);

        // Assert
        Assert.Equal(75, result.Score); // 50 (base) + 25 (user risk)
        Assert.Equal(RiskLevel.High, result.Level);
        Assert.Equal(25, result.Factors["user_risk_boost"]);
    }

    [Fact]
    public async Task CalculateRisk_WithAnomalyScore_ShouldApplyAnomalyBoost()
    {
        // Arrange
        var alert = CreateAlert(SeverityLevel.Medium);
        var envelope = CreateEventEnvelope(anomalyScore: 0.5);

        _mockTimeService.Setup(x => x.IsOffHours(It.IsAny<DateTimeOffset>())).Returns(false);
        _mockUserRiskService.Setup(x => x.GetUserRiskScore(It.IsAny<string>())).ReturnsAsync(0);

        // Act
        var result = await _service.CalculateRiskAsync(alert, envelope);

        // Assert
        Assert.Equal(60, result.Score); // 50 (base) + 10 (anomaly boost: 0.5 * 20)
        Assert.Equal(RiskLevel.Medium, result.Level);
        Assert.Equal(10, result.Factors["anomaly_boost"]);
    }

    [Fact]
    public async Task CalculateRisk_WithMultipleFactors_ShouldCombineCorrectly()
    {
        // Arrange
        var alert = CreateAlert(SeverityLevel.High);
        var envelope = CreateEventEnvelope(
            assetCriticality: AssetCriticality.High,
            threatIntelScore: new ThreatIntelScore { IsKnownMalicious = true, Score = 80 },
            username: "riskyuser",
            anomalyScore: 0.3);

        _mockTimeService.Setup(x => x.IsOffHours(It.IsAny<DateTimeOffset>())).Returns(true);
        _mockUserRiskService.Setup(x => x.GetUserRiskScore("riskyuser")).ReturnsAsync(15);

        // Act
        var result = await _service.CalculateRiskAsync(alert, envelope);

        // Assert
        Assert.Equal(100, result.Score); // Should be capped at 100
        Assert.Equal(RiskLevel.Critical, result.Level);
        Assert.True(result.Factors.ContainsKey("base_severity"));
        Assert.True(result.Factors.ContainsKey("asset_boost"));
        Assert.True(result.Factors.ContainsKey("threat_intel_boost"));
        Assert.True(result.Factors.ContainsKey("user_risk_boost"));
        Assert.True(result.Factors.ContainsKey("anomaly_boost"));
        Assert.Equal(1.2, result.Factors["time_of_day_multiplier"]);
    }

    [Fact]
    public async Task CalculateRisk_WithAllCriticalFactors_ShouldReturnMaximumScore()
    {
        // Arrange
        var alert = CreateAlert(SeverityLevel.Critical);
        var envelope = CreateEventEnvelope(
            assetCriticality: AssetCriticality.Critical,
            threatIntelScore: new ThreatIntelScore { IsKnownMalicious = true, Score = 100 },
            username: "criticaluser",
            anomalyScore: 1.0);

        _mockTimeService.Setup(x => x.IsOffHours(It.IsAny<DateTimeOffset>())).Returns(true);
        _mockUserRiskService.Setup(x => x.GetUserRiskScore("criticaluser")).ReturnsAsync(50);

        // Act
        var result = await _service.CalculateRiskAsync(alert, envelope);

        // Assert
        Assert.Equal(100, result.Score); // Should be capped at 100
        Assert.Equal(RiskLevel.Critical, result.Level);
    }

    [Fact]
    public async Task CalculateRisk_ShouldGenerateReasoning()
    {
        // Arrange
        var alert = CreateAlert(SeverityLevel.High);
        var envelope = CreateEventEnvelope(
            assetCriticality: AssetCriticality.Critical,
            threatIntelScore: new ThreatIntelScore { IsKnownMalicious = true, Score = 95 });

        _mockTimeService.Setup(x => x.IsOffHours(It.IsAny<DateTimeOffset>())).Returns(false);
        _mockUserRiskService.Setup(x => x.GetUserRiskScore(It.IsAny<string>())).ReturnsAsync(0);

        // Act
        var result = await _service.CalculateRiskAsync(alert, envelope);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(result.Reasoning));
        Assert.Contains("criticality", result.Reasoning.ToLowerInvariant());
        Assert.Contains("malicious", result.Reasoning.ToLowerInvariant());
    }

    private static AlertEntity CreateAlert(SeverityLevel severity)
    {
        return new AlertEntity
        {
            Id = Guid.NewGuid(),
            RuleId = "test-rule",
            RuleName = "Test Rule",
            Severity = severity.ToString().ToLowerInvariant(),
            Status = "new",
            TriggeredAt = DateTimeOffset.UtcNow,
            Source = "test"
        };
    }

    private static EventEnvelope CreateEventEnvelope(
        AssetCriticality? assetCriticality = null,
        ThreatIntelScore? threatIntelScore = null,
        string? username = null,
        double? anomalyScore = null)
    {
        var enrichment = new Dictionary<string, object>();

        if (assetCriticality.HasValue)
        {
            enrichment["source_asset"] = new
            {
                name = "TestAsset",
                criticality = assetCriticality.Value.ToString().ToLowerInvariant()
            };
        }

        if (threatIntelScore != null)
        {
            enrichment["threat_intel"] = threatIntelScore;
        }

        if (anomalyScore.HasValue)
        {
            enrichment["anomaly_score"] = anomalyScore.Value;
        }

        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            Source = "test",
            SourceType = "test",
            ReceivedAt = DateTimeOffset.UtcNow,
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                EventType = "test.event",
                Severity = "medium",
                Username = username ?? "testuser"
            },
            Enrichment = enrichment
        };
    }
}