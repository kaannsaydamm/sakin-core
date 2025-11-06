using Microsoft.Extensions.Logging;
using Moq;
using Sakin.Common.Models;
using Sakin.Correlation.Services;
using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Models;
using Sakin.Correlation.Persistence.Repositories;

namespace Sakin.Correlation.Tests.Services;

public class AlertCreatorServiceTests
{
    private readonly Mock<IAlertRepository> _mockAlertRepository;
    private readonly Mock<IAssetCacheService> _mockAssetCacheService;
    private readonly Mock<ILogger<AlertCreatorService>> _mockLogger;
    private readonly AlertCreatorService _alertCreatorService;

    public AlertCreatorServiceTests()
    {
        _mockAlertRepository = new Mock<IAlertRepository>();
        _mockAssetCacheService = new Mock<IAssetCacheService>();
        _mockLogger = new Mock<ILogger<AlertCreatorService>>();

        _alertCreatorService = new AlertCreatorService(
            _mockAlertRepository.Object,
            _mockAssetCacheService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CreateAlertAsync_ShouldBoostSeverity_WhenCriticalAssetInvolved()
    {
        // Arrange
        var rule = new CorrelationRule
        {
            Id = Guid.NewGuid(),
            Name = "Test Rule",
            Severity = SeverityLevel.Medium
        };

        var eventEnvelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            Normalized = new NormalizedEvent
            {
                SourceIp = "192.168.1.10",
                DestinationIp = "192.168.1.20"
            },
            Enrichment = new Dictionary<string, object>
            {
                ["source_asset"] = new
                {
                    id = Guid.NewGuid().ToString(),
                    name = "DC01",
                    criticality = "critical",
                    owner = "IT-Core"
                }
            }
        };

        var createdAlert = new AlertRecord
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            RuleName = rule.Name,
            Severity = SeverityLevel.High, // Should be boosted
            Status = AlertStatus.New,
            TriggeredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockAlertRepository.Setup(x => x.CreateAsync(It.IsAny<AlertRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdAlert);

        // Act
        await _alertCreatorService.CreateAlertAsync(rule, eventEnvelope);

        // Assert
        _mockAlertRepository.Verify(x => x.CreateAsync(
            It.Is<AlertRecord>(a => a.Severity == SeverityLevel.High), // Boosted from Medium to High
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAlertAsync_ShouldNotBoostSeverity_WhenNonCriticalAssetInvolved()
    {
        // Arrange
        var rule = new CorrelationRule
        {
            Id = Guid.NewGuid(),
            Name = "Test Rule",
            Severity = SeverityLevel.Medium
        };

        var eventEnvelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            Normalized = new NormalizedEvent
            {
                SourceIp = "192.168.1.100"
            },
            Enrichment = new Dictionary<string, object>
            {
                ["source_asset"] = new
                {
                    id = Guid.NewGuid().ToString(),
                    name = "LAB-01",
                    criticality = "low",
                    owner = "Dev-Team"
                }
            }
        };

        var createdAlert = new AlertRecord
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            RuleName = rule.Name,
            Severity = SeverityLevel.Medium, // Should NOT be boosted
            Status = AlertStatus.New,
            TriggeredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockAlertRepository.Setup(x => x.CreateAsync(It.IsAny<AlertRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdAlert);

        // Act
        await _alertCreatorService.CreateAlertAsync(rule, eventEnvelope);

        // Assert
        _mockAlertRepository.Verify(x => x.CreateAsync(
            It.Is<AlertRecord>(a => a.Severity == SeverityLevel.Medium), // Same as original
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAlertAsync_ShouldLookupAssetFromCache_WhenEnrichmentMissing()
    {
        // Arrange
        var rule = new CorrelationRule
        {
            Id = Guid.NewGuid(),
            Name = "Test Rule",
            Severity = SeverityLevel.Low
        };

        var eventEnvelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            Normalized = new NormalizedEvent
            {
                SourceIp = "192.168.1.10"
            },
            Enrichment = new Dictionary<string, object>() // No asset enrichment
        };

        var criticalAsset = new Asset
        {
            Id = Guid.NewGuid(),
            Name = "DC01",
            IpAddress = "192.168.1.10",
            Criticality = AssetCriticality.Critical
        };

        _mockAssetCacheService.Setup(x => x.GetAsset("192.168.1.10"))
            .Returns(criticalAsset);

        var createdAlert = new AlertRecord
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            RuleName = rule.Name,
            Severity = SeverityLevel.Medium, // Should be boosted from Low to Medium
            Status = AlertStatus.New,
            TriggeredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockAlertRepository.Setup(x => x.CreateAsync(It.IsAny<AlertRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdAlert);

        // Act
        await _alertCreatorService.CreateAlertAsync(rule, eventEnvelope);

        // Assert
        _mockAssetCacheService.Verify(x => x.GetAsset("192.168.1.10"), Times.Once);
        _mockAlertRepository.Verify(x => x.CreateAsync(
            It.Is<AlertRecord>(a => a.Severity == SeverityLevel.Medium), // Boosted
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(SeverityLevel.Low, SeverityLevel.Medium)]
    [InlineData(SeverityLevel.Medium, SeverityLevel.High)]
    [InlineData(SeverityLevel.High, SeverityLevel.Critical)]
    [InlineData(SeverityLevel.Critical, SeverityLevel.Critical)]
    public async Task CreateAlertAsync_ShouldBoostSeverityCorrectly(SeverityLevel original, SeverityLevel expected)
    {
        // Arrange
        var rule = new CorrelationRule
        {
            Id = Guid.NewGuid(),
            Name = "Test Rule",
            Severity = original
        };

        var eventEnvelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            Normalized = new NormalizedEvent
            {
                SourceIp = "192.168.1.10"
            },
            Enrichment = new Dictionary<string, object>
            {
                ["source_asset"] = new
                {
                    id = Guid.NewGuid().ToString(),
                    name = "Critical Server",
                    criticality = "critical"
                }
            }
        };

        var createdAlert = new AlertRecord
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            RuleName = rule.Name,
            Severity = expected,
            Status = AlertStatus.New,
            TriggeredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockAlertRepository.Setup(x => x.CreateAsync(It.IsAny<AlertRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdAlert);

        // Act
        await _alertCreatorService.CreateAlertAsync(rule, eventEnvelope);

        // Assert
        _mockAlertRepository.Verify(x => x.CreateAsync(
            It.Is<AlertRecord>(a => a.Severity == expected),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAlertAsync_ShouldIncludeAssetContextInAlert()
    {
        // Arrange
        var rule = new CorrelationRule
        {
            Id = Guid.NewGuid(),
            Name = "Test Rule",
            Severity = SeverityLevel.Medium
        };

        var eventEnvelope = new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            Normalized = new NormalizedEvent
            {
                SourceIp = "192.168.1.10",
                DestinationIp = "192.168.1.20"
            },
            Enrichment = new Dictionary<string, object>
            {
                ["source_asset"] = new
                {
                    id = Guid.NewGuid().ToString(),
                    name = "DC01",
                    criticality = "critical"
                }
            }
        };

        AlertRecord? capturedAlert = null;
        _mockAlertRepository.Setup(x => x.CreateAsync(It.IsAny<AlertRecord>(), It.IsAny<CancellationToken>()))
            .Callback<AlertRecord, CancellationToken>((alert, _) => capturedAlert = alert)
            .ReturnsAsync(new AlertRecord { Id = Guid.NewGuid() });

        // Act
        await _alertCreatorService.CreateAlertAsync(rule, eventEnvelope);

        // Assert
        Assert.NotNull(capturedAlert);
        Assert.NotNull(capturedAlert.Context);
        
        var context = capturedAlert.Context as Dictionary<string, object>;
        Assert.NotNull(context);
        
        Assert.True(context.ContainsKey("asset_context"));
        
        var assetContext = context["asset_context"] as Dictionary<string, object>;
        Assert.NotNull(assetContext);
        Assert.True(assetContext.ContainsKey("source_asset"));
    }
}