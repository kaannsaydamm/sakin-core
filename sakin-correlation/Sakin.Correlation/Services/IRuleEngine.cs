using Sakin.Common.Models;
using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services;

public interface IRuleEngine
{
    Task<IEnumerable<Alert>> EvaluateEventAsync(EventEnvelope eventEnvelope, CancellationToken cancellationToken);
}
