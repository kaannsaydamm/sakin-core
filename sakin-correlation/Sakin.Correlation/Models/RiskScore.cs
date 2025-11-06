namespace Sakin.Correlation.Models;

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public record RiskScore
{
    public int Score { get; init; } // 0-100
    public RiskLevel Level { get; init; } // Low, Medium, High, Critical
    public Dictionary<string, double> Factors { get; init; } = new(); // breakdown of scoring
    public string Reasoning { get; init; } = string.Empty; // human-readable explanation
}