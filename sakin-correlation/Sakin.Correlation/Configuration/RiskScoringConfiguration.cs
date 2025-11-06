namespace Sakin.Correlation.Configuration;

public class RiskScoringConfiguration
{
    public bool Enabled { get; set; } = true;
    public RiskScoringFactorsConfiguration Factors { get; set; } = new();
    public string BusinessHours { get; set; } = "09:00-17:00";
}

public class RiskScoringFactorsConfiguration
{
    public Dictionary<string, int> BaseWeights { get; set; } = new()
    {
        ["Low"] = 20,
        ["Medium"] = 50,
        ["High"] = 75,
        ["Critical"] = 100
    };

    public Dictionary<string, double> AssetMultipliers { get; set; } = new()
    {
        ["Low"] = 1.0,
        ["Medium"] = 1.2,
        ["High"] = 1.5,
        ["Critical"] = 2.0
    };

    public double OffHoursMultiplier { get; set; } = 1.2;
    public double ThreatIntelMaxBoost { get; set; } = 30;
    public double AnomalyMaxBoost { get; set; } = 20;
}