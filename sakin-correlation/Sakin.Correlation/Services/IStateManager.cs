using Sakin.Common.Models;
using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services;

public interface IStateManager
{
    Task<CorrelationState> AddEventAsync(string ruleId, string groupKey, Guid eventId, TimeSpan expiration, CancellationToken cancellationToken);

    Task<CorrelationState?> GetStateAsync(string ruleId, string groupKey, CancellationToken cancellationToken);

    Task ClearGroupAsync(string ruleId, string groupKey, CancellationToken cancellationToken);
}
