using FluentAssertions;
using Moq;
using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Models;
using Sakin.Correlation.Persistence.Repositories;
using Sakin.Panel.Api.Services;
using Xunit;

namespace Sakin.Panel.Api.Tests.Services;

public class AlertServiceTests
{
    private readonly Mock<IAlertRepository> _repositoryMock;
    private readonly AlertService _service;

    public AlertServiceTests()
    {
        _repositoryMock = new Mock<IAlertRepository>();
        _service = new AlertService(_repositoryMock.Object);
    }

    [Fact]
    public async Task GetAlertsAsync_ValidParams_ReturnsPagedAlerts()
    {
        var alerts = new List<AlertRecord>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RuleId = "rule1",
                RuleName = "Rule 1",
                Severity = SeverityLevel.High,
                Status = AlertStatus.New,
                TriggeredAt = DateTimeOffset.UtcNow,
                Context = new Dictionary<string, object?>(),
                MatchedConditions = new List<string>(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                RuleId = "rule2",
                RuleName = "Rule 2",
                Severity = SeverityLevel.Critical,
                Status = AlertStatus.New,
                TriggeredAt = DateTimeOffset.UtcNow,
                Context = new Dictionary<string, object?>(),
                MatchedConditions = new List<string>(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        _repositoryMock
            .Setup(r => r.GetAlertsAsync(1, 25, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((alerts, 2));

        var result = await _service.GetAlertsAsync(1, 25);

        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(25);
        result.TotalCount.Should().Be(2);
        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task GetAlertsAsync_WithSeverityFilter_ReturnFilteredAlerts()
    {
        var alerts = new List<AlertRecord>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RuleId = "rule1",
                RuleName = "Rule 1",
                Severity = SeverityLevel.Critical,
                Status = AlertStatus.New,
                TriggeredAt = DateTimeOffset.UtcNow,
                Context = new Dictionary<string, object?>(),
                MatchedConditions = new List<string>(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        _repositoryMock
            .Setup(r => r.GetAlertsAsync(1, 50, SeverityLevel.Critical, It.IsAny<CancellationToken>()))
            .ReturnsAsync((alerts, 1));

        var result = await _service.GetAlertsAsync(1, 50, SeverityLevel.Critical);

        result.Items.Should().HaveCount(1);
        result.Items[0].Severity.Should().Be("critical");
    }

    [Fact]
    public async Task GetAlertByIdAsync_ExistingAlert_ReturnsAlert()
    {
        var alertId = Guid.NewGuid();
        var alert = new AlertRecord
        {
            Id = alertId,
            RuleId = "rule1",
            RuleName = "Rule 1",
            Severity = SeverityLevel.High,
            Status = AlertStatus.New,
            TriggeredAt = DateTimeOffset.UtcNow,
            Context = new Dictionary<string, object?>(),
            MatchedConditions = new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        var result = await _service.GetAlertByIdAsync(alertId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(alertId);
    }

    [Fact]
    public async Task GetAlertByIdAsync_NonExistingAlert_ReturnsNull()
    {
        var alertId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertRecord?)null);

        var result = await _service.GetAlertByIdAsync(alertId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AcknowledgeAlertAsync_ExistingAlert_ReturnsAcknowledgedAlert()
    {
        var alertId = Guid.NewGuid();
        var alert = new AlertRecord
        {
            Id = alertId,
            RuleId = "rule1",
            RuleName = "Rule 1",
            Severity = SeverityLevel.High,
            Status = AlertStatus.Acknowledged,
            TriggeredAt = DateTimeOffset.UtcNow,
            Context = new Dictionary<string, object?>(),
            MatchedConditions = new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _repositoryMock
            .Setup(r => r.UpdateStatusAsync(alertId, AlertStatus.Acknowledged, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        var result = await _service.AcknowledgeAlertAsync(alertId);

        result.Should().NotBeNull();
        result!.Status.Should().Be("acknowledged");
    }

    [Fact]
    public async Task AcknowledgeAlertAsync_NonExistingAlert_ReturnsNull()
    {
        var alertId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.UpdateStatusAsync(alertId, AlertStatus.Acknowledged, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertRecord?)null);

        var result = await _service.AcknowledgeAlertAsync(alertId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAlertsAsync_CalculatesTotalPages_Correctly()
    {
        var alerts = new List<AlertRecord>();
        
        _repositoryMock
            .Setup(r => r.GetAlertsAsync(1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((alerts, 25));

        var result = await _service.GetAlertsAsync(1, 10);

        result.TotalPages.Should().Be(3);
    }
}
