using Sakin.Correlation.Models;
using Sakin.Common.Models;

namespace Sakin.Correlation.Services.State;

public interface IWindowStateStore
{
    Task<IReadOnlyList<EventEnvelope>> AddEventAsync(
        CorrelationRule rule,
        string groupKey,
        EventEnvelope envelope,
        TimeSpan windowSize,
        CancellationToken cancellationToken);
}
