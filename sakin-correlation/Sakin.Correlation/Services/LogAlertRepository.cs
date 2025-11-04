using Microsoft.Extensions.Logging;
using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services;

public class LogAlertRepository : IAlertRepository
{
    private readonly ILogger<LogAlertRepository> _logger;

    public LogAlertRepository(ILogger<LogAlertRepository> logger)
    {
        _logger = logger;
    }

    public Task PersistAsync(Alert alert, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "ALERT: {RuleName} (ID: {AlertId}, Severity: {Severity}) - {Description}. " +
            "Event Count: {EventCount}, Source IP: {SourceIp}, Event IDs: {EventIds}",
            alert.RuleName,
            alert.Id,
            alert.Severity,
            alert.Description,
            alert.EventCount,
            alert.SourceIp,
            string.Join(", ", alert.EventIds));

        return Task.CompletedTask;
    }
}
