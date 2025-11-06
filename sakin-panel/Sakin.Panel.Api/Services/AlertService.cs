using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Repositories;
using Sakin.Correlation.Services;
using Sakin.Panel.Api.Models;

namespace Sakin.Panel.Api.Services;

public class AlertService : IAlertService
{
    private readonly IAlertRepository _alertRepository;
    private readonly IAlertLifecycleService _lifecycleService;

    public AlertService(
        IAlertRepository alertRepository,
        IAlertLifecycleService lifecycleService)
    {
        _alertRepository = alertRepository;
        _lifecycleService = lifecycleService;
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

    public async Task<AlertResponse?> UpdateStatusAsync(
        Guid id,
        AlertStatus status,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default)
    {
        var alert = await _lifecycleService.TransitionStatusAsync(id, status, comment, user, cancellationToken);
        return alert is null ? null : AlertResponse.FromRecord(alert);
    }

    public async Task<AlertResponse?> StartInvestigationAsync(
        Guid id,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default)
    {
        var alert = await _lifecycleService.StartInvestigationAsync(id, comment, user, cancellationToken);
        return alert is null ? null : AlertResponse.FromRecord(alert);
    }

    public async Task<AlertResponse?> ResolveAsync(
        Guid id,
        string? reason = null,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default)
    {
        var alert = await _lifecycleService.ResolveAsync(id, reason, comment, user, cancellationToken);
        return alert is null ? null : AlertResponse.FromRecord(alert);
    }

    public async Task<AlertResponse?> CloseAsync(
        Guid id,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default)
    {
        var alert = await _lifecycleService.CloseAsync(id, comment, user, cancellationToken);
        return alert is null ? null : AlertResponse.FromRecord(alert);
    }

    public async Task<AlertResponse?> MarkFalsePositiveAsync(
        Guid id,
        string? reason = null,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default)
    {
        var alert = await _lifecycleService.MarkFalsePositiveAsync(id, reason, comment, user, cancellationToken);
        return alert is null ? null : AlertResponse.FromRecord(alert);
    }
}
