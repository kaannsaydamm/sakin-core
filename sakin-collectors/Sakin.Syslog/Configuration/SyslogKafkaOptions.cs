using Sakin.Messaging.Configuration;

namespace Sakin.Syslog.Configuration
{
    public class SyslogKafkaOptions
    {
        public const string SectionName = KafkaOptions.SectionName;

        public bool Enabled { get; set; } = true;

        public string Topic { get; set; } = "raw-events";

        public int BatchSize { get; set; } = 100;

        public int FlushIntervalMs { get; set; } = 5000;

        public int RetryCount { get; set; } = 3;

        public int RetryBackoffMs { get; set; } = 500;
    }
}