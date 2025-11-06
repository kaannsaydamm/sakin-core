using Sakin.Correlation.Models;
using Sakin.Panel.Api.Models;

namespace Sakin.Panel.Api.Services;

public interface IAlertService
{
    Task<PaginatedResponse<AlertResponse>> GetAlertsAsync(
        int page = 1,
        int pageSize = 50,
        SeverityLevel? severity = null,
        CancellationToken cancellationToken = default);

    Task<AlertResponse?> GetAlertByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<AlertResponse?> AcknowledgeAlertAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<AlertResponse?> UpdateStatusAsync(
        Guid id,
        AlertStatus status,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default);

    Task<AlertResponse?> StartInvestigationAsync(
        Guid id,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default);

    Task<AlertResponse?> ResolveAsync(
        Guid id,
        string? reason = null,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default);

    Task<AlertResponse?> CloseAsync(
        Guid id,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default);

    Task<AlertResponse?> MarkFalsePositiveAsync(
        Guid id,
        string? reason = null,
        string? comment = null,
        string? user = null,
        CancellationToken cancellationToken = default);
}
