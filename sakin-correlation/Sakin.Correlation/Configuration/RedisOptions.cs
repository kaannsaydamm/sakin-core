namespace Sakin.Correlation.Configuration;

public class RedisOptions
{
    public const string SectionName = "Redis";
    
    public string ConnectionString { get; set; } = "localhost:6379";
    public string KeyPrefix { get; set; } = "sakin:correlation:";
    public int DefaultTTL { get; set; } = 3600;
}

public class AggregationOptions
{
    public const string SectionName = "Aggregation";
    
    public int MaxWindowSize { get; set; } = 86400;
    public int CleanupInterval { get; set; } = 300;
}