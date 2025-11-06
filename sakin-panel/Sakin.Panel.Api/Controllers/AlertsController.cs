using Microsoft.AspNetCore.Mvc;
using Sakin.Correlation.Models;
using Sakin.Panel.Api.Models;
using Sakin.Panel.Api.Services;

namespace Sakin.Panel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;

    public AlertsController(IAlertService alertService)
    {
        _alertService = alertService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<AlertResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? severity = null,
        CancellationToken cancellationToken = default)
    {
        SeverityLevel? severityLevel = null;

        if (!string.IsNullOrWhiteSpace(severity))
        {
            if (!Enum.TryParse(severity, true, out SeverityLevel parsedSeverity))
            {
                return ValidationProblem(new ValidationProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid severity value",
                    Detail = $"'{severity}' is not a valid severity."
                });
            }

            severityLevel = parsedSeverity;
        }

        var alerts = await _alertService.GetAlertsAsync(page, pageSize, severityLevel, cancellationToken);
        return Ok(alerts);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var alert = await _alertService.GetAlertByIdAsync(id, cancellationToken);

        if (alert is null)
        {
            return NotFound();
        }

        return Ok(alert);
    }

    [HttpPost("{id:guid}/acknowledge")]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Acknowledge(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertService.AcknowledgeAlertAsync(id, cancellationToken);

        if (alert is null)
        {
            return NotFound();
        }

        return Ok(alert);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] StatusUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrEmpty(request.Status))
        {
            return BadRequest("Status is required");
        }

        if (!Enum.TryParse<AlertStatus>(request.Status, true, out var status))
        {
            return BadRequest($"'{request.Status}' is not a valid status");
        }

        var alert = await _alertService.UpdateStatusAsync(
            id, status, request.Comment, request.User, cancellationToken);

        if (alert is null)
        {
            return NotFound();
        }

        return Ok(alert);
    }

    [HttpPatch("{id:guid}/investigate")]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartInvestigation(
        Guid id,
        [FromBody] CommentRequest? request,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertService.StartInvestigationAsync(
            id, request?.Comment, request?.User, cancellationToken);

        if (alert is null)
        {
            return NotFound();
        }

        return Ok(alert);
    }

    [HttpPatch("{id:guid}/resolve")]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve(
        Guid id,
        [FromBody] ResolutionRequest? request,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertService.ResolveAsync(
            id, request?.Reason, request?.Comment, request?.User, cancellationToken);

        if (alert is null)
        {
            return NotFound();
        }

        return Ok(alert);
    }

    [HttpPatch("{id:guid}/close")]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Close(
        Guid id,
        [FromBody] CommentRequest? request,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertService.CloseAsync(
            id, request?.Comment, request?.User, cancellationToken);

        if (alert is null)
        {
            return NotFound();
        }

        return Ok(alert);
    }

    [HttpPatch("{id:guid}/false-positive")]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkFalsePositive(
        Guid id,
        [FromBody] ResolutionRequest? request,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertService.MarkFalsePositiveAsync(
            id, request?.Reason, request?.Comment, request?.User, cancellationToken);

        if (alert is null)
        {
            return NotFound();
        }

        return Ok(alert);
    }
}
