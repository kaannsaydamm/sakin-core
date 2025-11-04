namespace Sakin.Correlation.Configuration;

public class EngineSettings
{
    public const string SectionName = "Engine";

    public int MaxBackpressure { get; set; } = 1000;
    public int ProcessingTimeout { get; set; } = 30000;
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 500;
}
