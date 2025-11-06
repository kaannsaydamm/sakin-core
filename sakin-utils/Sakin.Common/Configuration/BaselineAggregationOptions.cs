namespace Sakin.Common.Configuration;

public class BaselineAggregationOptions
{
    public bool Enabled { get; set; } = true;
    public string KafkaTopic { get; set; } = "normalized-events";
    public string KafkaConsumerGroup { get; set; } = "clickhouse-sink";
    public int BatchSize { get; set; } = 1000;
    public int BatchTimeoutSeconds { get; set; } = 5;
    public int BaselineTtlHours { get; set; } = 25;
    public int AnalysisWindowDays { get; set; } = 7;
    public string ClickHouseConnectionString { get; set; } = "Host=localhost;Port=9000;Database=sakin;User=default;Password=";
}
