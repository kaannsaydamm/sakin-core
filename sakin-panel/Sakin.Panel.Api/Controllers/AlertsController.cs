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
}
