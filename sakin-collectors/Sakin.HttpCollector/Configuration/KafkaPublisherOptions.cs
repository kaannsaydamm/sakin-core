namespace Sakin.HttpCollector.Configuration;

public class KafkaPublisherOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "kafka:9092";
    public string Topic { get; set; } = "raw-events";
}
