namespace Sakin.Common.Models.SOAR;

public record ActiveResponseStep(
    string TargetAgentId,
    AgentCommandType Command,
    string Payload,
    int? TimeoutSeconds = 30
);