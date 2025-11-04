namespace Sakin.Correlation.Configuration;

public class CorrelationRulesOptions
{
    public const string SectionName = "CorrelationRules";

    public string RulesDirectory { get; set; } = "./rules";

    public int TimeWindowSeconds { get; set; } = 300;

    public int MaxEventsInWindow { get; set; } = 10000;

    public int MinEventsForCorrelation { get; set; } = 2;

    public int StateExpirationSeconds { get; set; } = 600;
}
