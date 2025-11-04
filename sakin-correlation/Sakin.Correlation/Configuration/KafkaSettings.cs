namespace Sakin.Correlation.Configuration;

public class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string NormalizedEventsTopic { get; set; } = "normalized-events";
    public string ConsumerGroup { get; set; } = "correlation-engine";
    public int BatchSize { get; set; } = 100;
    public int MaxConcurrency { get; set; } = 4;
    public string DeadLetterTopic { get; set; } = "normalized-events-dead-letter";
    public string ClientId { get; set; } = "sakin-correlation-engine";
}
