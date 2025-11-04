using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Sakin.Correlation.Models;
using Sakin.Panel.Api.Controllers;
using Sakin.Panel.Api.Models;
using Sakin.Panel.Api.Services;
using Xunit;

namespace Sakin.Panel.Api.Tests.Controllers;

public class AlertsControllerTests
{
    private readonly Mock<IAlertService> _serviceMock;
    private readonly AlertsController _controller;

    public AlertsControllerTests()
    {
        _serviceMock = new Mock<IAlertService>();
        _controller = new AlertsController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetAlerts_ValidRequest_ReturnsOk()
    {
        var response = new PaginatedResponse<AlertResponse>(
            new List<AlertResponse>(),
            1,
            25,
            0,
            0);

        _serviceMock
            .Setup(s => s.GetAlertsAsync(1, 25, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetAlerts(1, 25);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetAlerts_WithSeverityFilter_PassesFilterToService()
    {
        var response = new PaginatedResponse<AlertResponse>(
            new List<AlertResponse>(),
            1,
            25,
            0,
            0);

        _serviceMock
            .Setup(s => s.GetAlertsAsync(1, 25, SeverityLevel.High, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetAlerts(1, 25, "high");

        result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(s => s.GetAlertsAsync(1, 25, SeverityLevel.High, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAlerts_InvalidSeverity_ReturnsBadRequest()
    {
        var result = await _controller.GetAlerts(1, 25, "invalid");

        result.Should().BeOfType<BadRequestObjectResult>();
        var objectResult = result as BadRequestObjectResult;
        objectResult!.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    [Fact]
    public async Task Acknowledge_ExistingAlert_ReturnsOk()
    {
        var alertId = Guid.NewGuid();
        var response = new AlertResponse(
            alertId,
            "rule1",
            "Rule 1",
            "high",
            "acknowledged",
            DateTimeOffset.UtcNow,
            null,
            new Dictionary<string, object?>(),
            new List<string>(),
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        _serviceMock
            .Setup(s => s.AcknowledgeAlertAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.Acknowledge(alertId);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().Be(response);
    }

    [Fact]
    public async Task Acknowledge_NonExistingAlert_ReturnsNotFound()
    {
        var alertId = Guid.NewGuid();

        _serviceMock
            .Setup(s => s.AcknowledgeAlertAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertResponse?)null);

        var result = await _controller.Acknowledge(alertId);

        result.Should().BeOfType<NotFoundResult>();
    }
}
