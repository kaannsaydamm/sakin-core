namespace Sakin.Core.Sensor.Configuration
{
    public class SensorOptions
    {
        public const string SectionName = "Sensor";

        public TopicsOptions Topics { get; set; } = new();
        public FallbackOptions Fallback { get; set; } = new();
        public int MetricsLogIntervalSeconds { get; set; } = 30;

        public class TopicsOptions
        {
            public string NetworkEvents { get; set; } = "network-events";
        }

        public class FallbackOptions
        {
            public string Path { get; set; } = "/data/exports/sensor-kafka-fallback.jsonl";
        }
    }
}
