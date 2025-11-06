using Sakin.Correlation.Models;

namespace Sakin.Common.Models.SOAR;

public record AlertActionMessage(
    AlertEntity Alert,
    CorrelationRuleV2 Rule
);