using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Repositories;
using Sakin.Panel.Api.Models;

namespace Sakin.Panel.Api.Services;

public class AlertService : IAlertService
{
    private readonly IAlertRepository _alertRepository;

    public AlertService(IAlertRepository alertRepository)
    {
        _alertRepository = alertRepository;
    }

    public async Task<PaginatedResponse<AlertResponse>> GetAlertsAsync(
        int page = 1,
        int pageSize = 50,
        SeverityLevel? severity = null,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 50;
        }

        var (alerts, totalCount) = await _alertRepository.GetAlertsAsync(page, pageSize, severity, cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = alerts
            .Select(AlertResponse.FromRecord)
            .ToList();

        return new PaginatedResponse<AlertResponse>(
            items,
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    public async Task<AlertResponse?> GetAlertByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertRepository.GetByIdAsync(id, cancellationToken);
        return alert is null ? null : AlertResponse.FromRecord(alert);
    }

    public async Task<AlertResponse?> AcknowledgeAlertAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertRepository.UpdateStatusAsync(id, AlertStatus.Acknowledged, cancellationToken);
        return alert is null ? null : AlertResponse.FromRecord(alert);
    }
}
