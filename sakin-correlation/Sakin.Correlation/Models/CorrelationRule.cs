using Sakin.Common.Models;

namespace Sakin.Correlation.Models;

public record CorrelationRule
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public Severity Severity { get; init; } = Severity.Medium;

    public bool Enabled { get; init; } = true;

    public int MinEventCount { get; init; } = 2;

    public int TimeWindowSeconds { get; init; } = 300;

    public List<RuleCondition> Conditions { get; init; } = new();

    public List<string> GroupByFields { get; init; } = new();

    public List<string> Tags { get; init; } = new();
}
