using Sakin.Correlation.Models;
using Sakin.Common.Models;

namespace Sakin.Correlation.Engine;

public interface IRuleEvaluatorV2 : IRuleEvaluator
{
    Task<EvaluationResult> EvaluateAsync(CorrelationRuleV2 rule, EventEnvelope eventEnvelope);
}