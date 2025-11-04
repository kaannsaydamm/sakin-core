using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services.Rules;

public interface IRuleProvider
{
    IReadOnlyCollection<CorrelationRule> GetRules();
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
