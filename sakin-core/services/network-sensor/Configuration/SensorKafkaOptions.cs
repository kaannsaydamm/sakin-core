using Sakin.Messaging.Configuration;

namespace Sakin.Core.Sensor.Configuration;

public class SensorKafkaOptions
{
    public const string SectionName = KafkaOptions.SectionName;

    public bool Enabled { get; set; } = false;

    public string RawEventsTopic { get; set; } = "raw-events";

    public int BatchSize { get; set; } = 100;

    public int FlushIntervalMs { get; set; } = 1000;

    public int RetryCount { get; set; } = 3;

    public int RetryBackoffMs { get; set; } = 200;
}
