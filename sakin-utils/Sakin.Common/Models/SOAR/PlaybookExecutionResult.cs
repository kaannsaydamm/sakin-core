namespace Sakin.Common.Models.SOAR;

public record PlaybookExecutionResult(
    Guid ExecutionId,
    string PlaybookId,
    bool Success,
    List<StepExecutionResult> StepResults,
    string? ErrorMessage = null,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc
);

public record StepExecutionResult(
    string StepId,
    bool Success,
    string? Output = null,
    string? ErrorMessage = null,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc
);