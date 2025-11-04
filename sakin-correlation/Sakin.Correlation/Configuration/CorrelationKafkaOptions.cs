using Sakin.Messaging.Configuration;

namespace Sakin.Correlation.Configuration;

public class CorrelationKafkaOptions
{
    public const string SectionName = KafkaOptions.SectionName;

    public string NormalizedEventsTopic { get; set; } = "normalized-events";

    public string AlertsTopic { get; set; } = "alerts";

    public string ConsumerGroup { get; set; } = "correlation-service";
}
