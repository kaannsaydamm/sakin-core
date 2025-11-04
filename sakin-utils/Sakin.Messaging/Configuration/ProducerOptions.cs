namespace Sakin.Messaging.Configuration
{
    public class ProducerOptions
    {
        public const string SectionName = "KafkaProducer";

        public string DefaultTopic { get; set; } = "events";
        public int BatchSize { get; set; } = 100;
        public int LingerMs { get; set; } = 10;
        public CompressionType CompressionType { get; set; } = CompressionType.Snappy;
        public Acks RequiredAcks { get; set; } = Acks.Leader;
        public int RetryCount { get; set; } = 3;
        public int RetryBackoffMs { get; set; } = 100;
        public bool EnableIdempotence { get; set; } = true;
        public int MaxInFlight { get; set; } = 5;
    }

    public enum CompressionType
    {
        None,
        Gzip,
        Snappy,
        Lz4,
        Zstd
    }

    public enum Acks
    {
        None = 0,
        Leader = 1,
        All = -1
    }
}
