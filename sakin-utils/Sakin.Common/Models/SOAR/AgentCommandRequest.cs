namespace Sakin.Common.Models.SOAR;

public record AgentCommandRequest(
    Guid CorrelationId,
    string TargetAgentId,
    AgentCommandType Command,
    string Payload,
    DateTime ExpireAtUtc
);