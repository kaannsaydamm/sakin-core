namespace Sakin.Common.Models.SOAR;

public record PlaybookDefinition(
    string Id,
    string Name,
    string Description,
    List<PlaybookStep> Steps,
    bool Enabled = true,
    string? Version = null
);