namespace Sakin.Correlation.Models;

public enum RuleOperator
{
    Equals,
    Contains,
    StartsWith,
    EndsWith
}

public record RuleCondition
{
    public string Field { get; init; } = string.Empty;

    public RuleOperator Operator { get; init; } = RuleOperator.Equals;

    public string Value { get; init; } = string.Empty;
}
