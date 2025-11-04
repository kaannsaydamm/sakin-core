namespace Sakin.Messaging.Configuration
{
    public class KafkaOptions
    {
        public const string SectionName = "Kafka";

        public string BootstrapServers { get; set; } = "localhost:9092";
        public int RequestTimeoutMs { get; set; } = 30000;
        public int MessageTimeoutMs { get; set; } = 60000;
        public string ClientId { get; set; } = "sakin-client";
        public SecurityProtocol SecurityProtocol { get; set; } = SecurityProtocol.Plaintext;
        public string? SaslMechanism { get; set; }
        public string? SaslUsername { get; set; }
        public string? SaslPassword { get; set; }
    }

    public enum SecurityProtocol
    {
        Plaintext,
        Ssl,
        SaslPlaintext,
        SaslSsl
    }
}
