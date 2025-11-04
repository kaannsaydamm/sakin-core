using Sakin.Messaging.Configuration;

namespace Sakin.Ingest.Configuration;

public class IngestKafkaOptions
{
    public const string SectionName = KafkaOptions.SectionName;

    public string RawEventsTopic { get; set; } = "raw-events";

    public string NormalizedEventsTopic { get; set; } = "normalized-events";

    public string ConsumerGroup { get; set; } = "ingest-service";
}
