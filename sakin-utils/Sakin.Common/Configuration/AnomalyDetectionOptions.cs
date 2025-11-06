namespace Sakin.Common.Configuration;

public class AnomalyDetectionOptions
{
    public bool Enabled { get; set; } = true;
    public double ZScoreThreshold { get; set; } = 2.5;
    public int CacheDurationSeconds { get; set; } = 60;
    public double AnomalyMaxBoost { get; set; } = 20.0;
    public string RedisKeyPrefix { get; set; } = "sakin:baseline";
}
