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
}
