namespace Sakin.Correlation.Configuration;

public class KafkaWorkerOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";

    public string Topic { get; set; } = "normalized-events";

    public string ConsumerGroup { get; set; } = "correlation-engine";

    public string ClientId { get; set; } = "sakin-correlation-worker";
}
