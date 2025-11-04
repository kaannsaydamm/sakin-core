namespace Sakin.Messaging.Configuration
{
    public class ConsumerOptions
    {
        public const string SectionName = "KafkaConsumer";

        public string GroupId { get; set; } = "sakin-consumer-group";
        public string[] Topics { get; set; } = Array.Empty<string>();
        public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Earliest;
        public bool EnableAutoCommit { get; set; } = false;
        public int AutoCommitIntervalMs { get; set; } = 5000;
        public int SessionTimeoutMs { get; set; } = 10000;
        public int MaxPollIntervalMs { get; set; } = 300000;
        public int FetchMinBytes { get; set; } = 1;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 1000;
    }

    public enum AutoOffsetReset
    {
        Earliest,
        Latest,
        Error
    }
}
