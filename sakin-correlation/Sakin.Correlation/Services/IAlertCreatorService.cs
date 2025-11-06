using Sakin.Common.Models;
using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services;

public interface IAlertCreatorService
{
    Task CreateAlertAsync(CorrelationRule rule, EventEnvelope eventEnvelope, CancellationToken cancellationToken = default);
}

public interface IAlertCreatorServiceWithRiskScoring : IAlertCreatorService
{
    Task CreateAlertWithRiskScoringAsync(CorrelationRule rule, EventEnvelope eventEnvelope, CancellationToken cancellationToken = default);
    Task CreateAlertWithPlaybookActionsAsync(CorrelationRuleV2 rule, EventEnvelope eventEnvelope, CancellationToken cancellationToken = default);
}