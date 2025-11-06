namespace Sakin.Common.Models.SOAR;

public record PlaybookStep(
    string Id,
    string Action,
    Dictionary<string, object> Parameters,
    string? Condition = null,
    int? RetryCount = null,
    TimeSpan? RetryDelay = null
);