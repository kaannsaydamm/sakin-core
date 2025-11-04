using FluentAssertions;
using Sakin.Messaging.Configuration;

namespace Sakin.Messaging.Tests.Configuration
{
    public class KafkaOptionsTests
    {
        [Fact]
        public void KafkaOptions_HasCorrectDefaults()
        {
            var options = new KafkaOptions();

            options.BootstrapServers.Should().Be("localhost:9092");
            options.ClientId.Should().Be("sakin-client");
            options.RequestTimeoutMs.Should().Be(30000);
            options.MessageTimeoutMs.Should().Be(60000);
            options.SecurityProtocol.Should().Be(SecurityProtocol.Plaintext);
        }

        [Fact]
        public void KafkaOptions_CanSetProperties()
        {
            var options = new KafkaOptions
            {
                BootstrapServers = "kafka:9092",
                ClientId = "test-client",
                RequestTimeoutMs = 10000,
                MessageTimeoutMs = 20000,
                SecurityProtocol = SecurityProtocol.Ssl
            };

            options.BootstrapServers.Should().Be("kafka:9092");
            options.ClientId.Should().Be("test-client");
            options.RequestTimeoutMs.Should().Be(10000);
            options.MessageTimeoutMs.Should().Be(20000);
            options.SecurityProtocol.Should().Be(SecurityProtocol.Ssl);
        }

        [Fact]
        public void ProducerOptions_HasCorrectDefaults()
        {
            var options = new ProducerOptions();

            options.DefaultTopic.Should().Be("events");
            options.BatchSize.Should().Be(100);
            options.LingerMs.Should().Be(10);
            options.CompressionType.Should().Be(CompressionType.Snappy);
            options.RequiredAcks.Should().Be(Acks.Leader);
            options.RetryCount.Should().Be(3);
            options.RetryBackoffMs.Should().Be(100);
            options.EnableIdempotence.Should().BeTrue();
            options.MaxInFlight.Should().Be(5);
        }

        [Fact]
        public void ConsumerOptions_HasCorrectDefaults()
        {
            var options = new ConsumerOptions();

            options.GroupId.Should().Be("sakin-consumer-group");
            options.Topics.Should().BeEmpty();
            options.AutoOffsetReset.Should().Be(AutoOffsetReset.Earliest);
            options.EnableAutoCommit.Should().BeFalse();
            options.AutoCommitIntervalMs.Should().Be(5000);
            options.SessionTimeoutMs.Should().Be(10000);
            options.MaxPollIntervalMs.Should().Be(300000);
            options.MaxRetries.Should().Be(3);
            options.RetryDelayMs.Should().Be(1000);
        }

        [Fact]
        public void ConsumerOptions_CanSetTopics()
        {
            var options = new ConsumerOptions
            {
                Topics = new[] { "topic1", "topic2", "topic3" }
            };

            options.Topics.Should().HaveCount(3);
            options.Topics.Should().Contain("topic1");
            options.Topics.Should().Contain("topic2");
            options.Topics.Should().Contain("topic3");
        }

        [Fact]
        public void SecurityProtocol_HasAllValues()
        {
            var values = Enum.GetValues<SecurityProtocol>();

            values.Should().Contain(SecurityProtocol.Plaintext);
            values.Should().Contain(SecurityProtocol.Ssl);
            values.Should().Contain(SecurityProtocol.SaslPlaintext);
            values.Should().Contain(SecurityProtocol.SaslSsl);
        }

        [Fact]
        public void CompressionType_HasAllValues()
        {
            var values = Enum.GetValues<CompressionType>();

            values.Should().Contain(CompressionType.None);
            values.Should().Contain(CompressionType.Gzip);
            values.Should().Contain(CompressionType.Snappy);
            values.Should().Contain(CompressionType.Lz4);
            values.Should().Contain(CompressionType.Zstd);
        }
    }
}
