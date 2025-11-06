using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services;

public interface IRuleLoaderService
{
    IReadOnlyList<CorrelationRule> Rules { get; }
    Task LoadRulesAsync(CancellationToken cancellationToken = default);
    Task ReloadRulesAsync(CancellationToken cancellationToken = default);
}