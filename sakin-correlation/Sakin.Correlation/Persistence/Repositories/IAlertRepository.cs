using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Models;

namespace Sakin.Correlation.Persistence.Repositories;

public interface IAlertRepository
{
    Task<AlertRecord> CreateAsync(AlertRecord alert, CancellationToken cancellationToken = default);

    Task<AlertRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertRecord>> GetRecentAlertsAsync(
        DateTimeOffset since,
        SeverityLevel? severity = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertRecord>> GetAlertsByRuleAsync(
        string ruleId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AlertRecord> Alerts, int TotalCount)> GetAlertsAsync(
        int page = 1,
        int pageSize = 50,
        SeverityLevel? severity = null,
        CancellationToken cancellationToken = default);

    Task<AlertRecord?> UpdateStatusAsync(
        Guid id,
        AlertStatus status,
        CancellationToken cancellationToken = default);
}
