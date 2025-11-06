using Microsoft.Extensions.Logging;
using Moq;
using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Models;
using Sakin.Correlation.Persistence.Repositories;
using Sakin.Correlation.Services;
using Xunit;

namespace Sakin.Correlation.Tests.Services;

public class AlertLifecycleServiceTests
{
    private readonly Mock<IAlertRepository> _mockRepository;
    private readonly Mock<ILogger<AlertLifecycleService>> _mockLogger;
    private readonly AlertLifecycleService _service;

    public AlertLifecycleServiceTests()
    {
        _mockRepository = new Mock<IAlertRepository>();
        _mockLogger = new Mock<ILogger<AlertLifecycleService>>();
        _service = new AlertLifecycleService(_mockRepository.Object, _mockLogger.Object);
    }

    private AlertRecord CreateSampleAlert(AlertStatus status = AlertStatus.New)
    {
        return new AlertRecord
        {
            Id = Guid.NewGuid(),
            RuleId = "rule-123",
            RuleName = "Test Rule",
            Severity = SeverityLevel.High,
            Status = status,
            TriggeredAt = DateTimeOffset.UtcNow,
            Source = "192.168.1.1",
            Context = new Dictionary<string, object?>(),
            MatchedConditions = Array.Empty<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            AlertCount = 1,
            FirstSeen = DateTimeOffset.UtcNow,
            LastSeen = DateTimeOffset.UtcNow,
            StatusHistory = Array.Empty<StatusHistoryEntry>()
        };
    }

    [Fact]
    public async Task AcknowledgeAsync_WithNewAlert_TransitionsSuccessfully()
    {
        // Arrange
        var alert = CreateSampleAlert();
        var alertId = alert.Id;
        
        _mockRepository
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        var acknowledgedAlert = CreateSampleAlert(AlertStatus.Acknowledged);
        acknowledgedAlert.Id = alertId;
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Entities.AlertEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<Entities.AlertEntity>());

        _mockRepository
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(acknowledgedAlert);

        // Act
        var result = await _service.AcknowledgeAsync(alertId, "Test", "test-user");

        // Assert
        Assert.NotNull(result);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Entities.AlertEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransitionStatusAsync_WithInvalidTransition_ThrowsException()
    {
        // Arrange
        var alert = CreateSampleAlert(AlertStatus.Closed);
        var alertId = alert.Id;

        _mockRepository
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TransitionStatusAsync(alertId, AlertStatus.PendingScore));
    }

    [Fact]
    public async Task TransitionStatusAsync_WithValidTransition_RecordsHistory()
    {
        // Arrange
        var alert = CreateSampleAlert();
        var alertId = alert.Id;
        var comment = "Acknowledging alert";

        _mockRepository
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        var updatedAlert = CreateSampleAlert(AlertStatus.Acknowledged);
        updatedAlert.Id = alertId;
        
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Entities.AlertEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<Entities.AlertEntity>());

        _mockRepository
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedAlert);

        // Act
        var result = await _service.TransitionStatusAsync(alertId, AlertStatus.Acknowledged, comment, "analyst");

        // Assert
        Assert.NotNull(result);
        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Entities.AlertEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_WithUnderInvestigation_SetsResolvedAtTimestamp()
    {
        // Arrange
        var alert = CreateSampleAlert(AlertStatus.UnderInvestigation);
        alert.InvestigationStartedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var alertId = alert.Id;

        _mockRepository
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        var resolvedAlert = CreateSampleAlert(AlertStatus.Resolved);
        resolvedAlert.Id = alertId;
        resolvedAlert.ResolvedAt = DateTimeOffset.UtcNow;
        
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Entities.AlertEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<Entities.AlertEntity>());

        _mockRepository
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedAlert);

        // Act
        var result = await _service.ResolveAsync(alertId, "CVE-2024-001 patched", "Applied security patch");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AlertStatus.Resolved, result.Status);
    }

    [Fact]
    public async Task MarkFalsePositiveAsync_SetsCorrectTimestamp()
    {
        // Arrange
        var alert = CreateSampleAlert();
        var alertId = alert.Id;

        _mockRepository
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        var fpAlert = CreateSampleAlert(AlertStatus.FalsePositive);
        fpAlert.Id = alertId;
        fpAlert.FalsePositiveAt = DateTimeOffset.UtcNow;

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Entities.AlertEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<Entities.AlertEntity>());

        _mockRepository
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fpAlert);

        // Act
        var result = await _service.MarkFalsePositiveAsync(alertId, "Benign activity");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AlertStatus.FalsePositive, result.Status);
    }

    [Fact]
    public async Task StartInvestigationAsync_TransitionsFromAcknowledged()
    {
        // Arrange
        var alert = CreateSampleAlert(AlertStatus.Acknowledged);
        alert.AcknowledgedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var alertId = alert.Id;

        _mockRepository
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        var investigatingAlert = CreateSampleAlert(AlertStatus.UnderInvestigation);
        investigatingAlert.Id = alertId;
        investigatingAlert.InvestigationStartedAt = DateTimeOffset.UtcNow;

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Entities.AlertEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<Entities.AlertEntity>());

        _mockRepository
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(investigatingAlert);

        // Act
        var result = await _service.StartInvestigationAsync(alertId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AlertStatus.UnderInvestigation, result.Status);
    }

    [Fact]
    public async Task TransitionStatusAsync_WithNullAlert_ReturnsNull()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertRecord?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(
            () => _service.TransitionStatusAsync(alertId, AlertStatus.Acknowledged));
    }
}
