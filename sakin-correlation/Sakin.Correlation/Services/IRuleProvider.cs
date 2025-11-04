using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services;

public interface IRuleProvider
{
    Task<IReadOnlyList<CorrelationRule>> GetRulesAsync(CancellationToken cancellationToken);
}
