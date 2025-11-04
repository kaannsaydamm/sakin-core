namespace Sakin.Correlation.Exceptions;

public class RuleParsingException : Exception
{
    public string? RuleId { get; }
    public string? PropertyPath { get; }

    public RuleParsingException(string message) : base(message)
    {
    }

    public RuleParsingException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public RuleParsingException(string ruleId, string message) : base(message)
    {
        RuleId = ruleId;
    }

    public RuleParsingException(string ruleId, string propertyPath, string message) : base(message)
    {
        RuleId = ruleId;
        PropertyPath = propertyPath;
    }

    public RuleParsingException(string ruleId, string propertyPath, string message, Exception innerException) : base(message, innerException)
    {
        RuleId = ruleId;
        PropertyPath = propertyPath;
    }
}