using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services;

public interface IRuleLoaderServiceV2 : IRuleLoaderService
{
    IReadOnlyList<CorrelationRuleV2> RulesV2 { get; }
}