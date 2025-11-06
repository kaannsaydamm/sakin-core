using Microsoft.AspNetCore.Mvc;
using Moq;
using Sakin.Correlation.Models;
using Sakin.Panel.Api.Controllers;
using Sakin.Panel.Api.Models;
using Sakin.Panel.Api.Services;
using Xunit;

namespace Sakin.Panel.Api.Tests.Controllers;

public class AlertsControllerLifecycleTests
{
    private readonly Mock<IAlertService> _mockAlertService;
    private readonly AlertsController _controller;

    public AlertsControllerLifecycleTests()
    {
        _mockAlertService = new Mock<IAlertService>();
        _controller = new AlertsController(_mockAlertService.Object);
    }

    private AlertResponse CreateSampleAlertResponse(string status = "new")
    {
        return new AlertResponse(
            Guid.NewGuid(),
            "rule-123",
            "Test Rule",
            "high",
            status,
            DateTimeOffset.UtcNow,
            "192.168.1.1",
            new Dictionary<string, object?>(),
            Array.Empty<string>(),
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            Array.Empty<StatusHistoryEntryDto>(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    [Fact]
    public async Task GetById_WithValidId_ReturnsAlertResponse()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var expectedAlert = CreateSampleAlertResponse();
        _mockAlertService
            .Setup(s => s.GetAlertByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAlert);

        // Act
        var result = await _controller.GetById(alertId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedAlert = Assert.IsType<AlertResponse>(okResult.Value);
        Assert.Equal(expectedAlert.RuleId, returnedAlert.RuleId);
    }

    [Fact]
    public async Task GetById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        _mockAlertService
            .Setup(s => s.GetAlertByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertResponse?)null);

        // Act
        var result = await _controller.GetById(alertId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_WithValidTransition_ReturnsUpdatedAlert()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var request = new StatusUpdateRequest
        {
            Status = "acknowledged",
            Comment = "Test",
            User = "analyst"
        };
        var updatedAlert = CreateSampleAlertResponse("acknowledged");
        _mockAlertService
            .Setup(s => s.UpdateStatusAsync(alertId, AlertStatus.Acknowledged, "Test", "analyst", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedAlert);

        // Act
        var result = await _controller.UpdateStatus(alertId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedAlert = Assert.IsType<AlertResponse>(okResult.Value);
        Assert.Equal("acknowledged", returnedAlert.Status);
    }

    [Fact]
    public async Task UpdateStatus_WithInvalidStatus_ReturnsBadRequest()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var request = new StatusUpdateRequest
        {
            Status = "invalid_status"
        };

        // Act
        var result = await _controller.UpdateStatus(alertId, request);

        // Assert
        Assert.IsType<BadObjectResult>(result);
    }

    [Fact]
    public async Task StartInvestigation_TransitionsToUnderInvestigation()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var request = new CommentRequest { Comment = "Starting investigation", User = "analyst" };
        var investigatingAlert = CreateSampleAlertResponse("under_investigation");
        _mockAlertService
            .Setup(s => s.StartInvestigationAsync(alertId, "Starting investigation", "analyst", It.IsAny<CancellationToken>()))
            .ReturnsAsync(investigatingAlert);

        // Act
        var result = await _controller.StartInvestigation(alertId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedAlert = Assert.IsType<AlertResponse>(okResult.Value);
        Assert.Equal("under_investigation", returnedAlert.Status);
    }

    [Fact]
    public async Task Resolve_WithReason_ReturnsResolvedAlert()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var request = new ResolutionRequest 
        { 
            Reason = "Issue remediated",
            Comment = "Applied patch",
            User = "analyst"
        };
        var resolvedAlert = CreateSampleAlertResponse("resolved");
        _mockAlertService
            .Setup(s => s.ResolveAsync(alertId, "Issue remediated", "Applied patch", "analyst", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedAlert);

        // Act
        var result = await _controller.Resolve(alertId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedAlert = Assert.IsType<AlertResponse>(okResult.Value);
        Assert.Equal("resolved", returnedAlert.Status);
    }

    [Fact]
    public async Task Close_TransitionsToClosedStatus()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var request = new CommentRequest { Comment = "Closing alert", User = "analyst" };
        var closedAlert = CreateSampleAlertResponse("closed");
        _mockAlertService
            .Setup(s => s.CloseAsync(alertId, "Closing alert", "analyst", It.IsAny<CancellationToken>()))
            .ReturnsAsync(closedAlert);

        // Act
        var result = await _controller.Close(alertId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedAlert = Assert.IsType<AlertResponse>(okResult.Value);
        Assert.Equal("closed", returnedAlert.Status);
    }

    [Fact]
    public async Task MarkFalsePositive_TransitionsToFalsePositiveStatus()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var request = new ResolutionRequest 
        { 
            Reason = "Authorized activity",
            Comment = "Maintenance window",
            User = "analyst"
        };
        var fpAlert = CreateSampleAlertResponse("false_positive");
        _mockAlertService
            .Setup(s => s.MarkFalsePositiveAsync(alertId, "Authorized activity", "Maintenance window", "analyst", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fpAlert);

        // Act
        var result = await _controller.MarkFalsePositive(alertId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedAlert = Assert.IsType<AlertResponse>(okResult.Value);
        Assert.Equal("false_positive", returnedAlert.Status);
    }

    [Fact]
    public async Task UpdateStatus_WithMissingAlert_ReturnsNotFound()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var request = new StatusUpdateRequest
        {
            Status = "acknowledged",
            Comment = "Test"
        };
        _mockAlertService
            .Setup(s => s.UpdateStatusAsync(alertId, AlertStatus.Acknowledged, "Test", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertResponse?)null);

        // Act
        var result = await _controller.UpdateStatus(alertId, request);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}
