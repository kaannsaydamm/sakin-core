namespace Sakin.Common.Models.SOAR;

public record AgentCommandResult(
    Guid CorrelationId,
    string AgentId,
    bool Success,
    string Output,
    DateTime ExecutedAtUtc
);