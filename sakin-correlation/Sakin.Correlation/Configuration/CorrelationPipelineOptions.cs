namespace Sakin.Correlation.Configuration;

public class CorrelationPipelineOptions
{
    public const string SectionName = "CorrelationPipeline";

    public int MaxDegreeOfParallelism { get; set; } = 4;

    public int ChannelCapacity { get; set; } = 256;

    public int BatchSize { get; set; } = 50;

    public int BatchIntervalMilliseconds { get; set; } = 1000;
}
