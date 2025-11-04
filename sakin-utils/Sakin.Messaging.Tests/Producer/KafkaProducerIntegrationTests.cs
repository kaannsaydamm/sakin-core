using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;
using Testcontainers.Kafka;

namespace Sakin.Messaging.Tests.Producer
{
    public class KafkaProducerIntegrationTests : IAsyncLifetime
    {
        private KafkaContainer? _kafkaContainer;
        private string _bootstrapServers = string.Empty;

        public async Task InitializeAsync()
        {
            _kafkaContainer = new KafkaBuilder()
                .WithImage("confluentinc/cp-kafka:7.5.0")
                .Build();

            await _kafkaContainer.StartAsync();
            _bootstrapServers = _kafkaContainer.GetBootstrapAddress();
        }

        public async Task DisposeAsync()
        {
            if (_kafkaContainer != null)
            {
                await _kafkaContainer.DisposeAsync();
            }
        }

        [Fact]
        public async Task ProduceAsync_WithValidMessage_SuccessfullyProduces()
        {
            var kafkaOptions = Options.Create(new KafkaOptions
            {
                BootstrapServers = _bootstrapServers,
                ClientId = "test-producer"
            });

            var producerOptions = Options.Create(new ProducerOptions
            {
                DefaultTopic = "test-topic",
                RetryCount = 3
            });

            var serializer = new JsonMessageSerializer();
            var logger = Mock.Of<ILogger<KafkaProducer>>();

            using var producer = new KafkaProducer(kafkaOptions, producerOptions, serializer, logger);

            var testMessage = new TestMessage { Id = "123", Name = "Test", Value = 42 };

            var result = await producer.ProduceAsync("test-topic", testMessage, "key-123");

            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Topic.Should().Be("test-topic");
            result.Partition.Should().BeGreaterOrEqualTo(0);
            result.Offset.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public async Task ProduceAsync_WithDefaultTopic_SuccessfullyProduces()
        {
            var kafkaOptions = Options.Create(new KafkaOptions
            {
                BootstrapServers = _bootstrapServers,
                ClientId = "test-producer"
            });

            var producerOptions = Options.Create(new ProducerOptions
            {
                DefaultTopic = "default-topic"
            });

            var serializer = new JsonMessageSerializer();
            var logger = Mock.Of<ILogger<KafkaProducer>>();

            using var producer = new KafkaProducer(kafkaOptions, producerOptions, serializer, logger);

            var testMessage = new TestMessage { Id = "456", Name = "Default", Value = 99 };

            var result = await producer.ProduceAsync(testMessage);

            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Topic.Should().Be("default-topic");
        }

        [Fact]
        public async Task ProduceAsync_MultipleMessages_AllSucceed()
        {
            var kafkaOptions = Options.Create(new KafkaOptions
            {
                BootstrapServers = _bootstrapServers,
                ClientId = "test-producer"
            });

            var producerOptions = Options.Create(new ProducerOptions
            {
                DefaultTopic = "batch-topic",
                BatchSize = 10,
                LingerMs = 5
            });

            var serializer = new JsonMessageSerializer();
            var logger = Mock.Of<ILogger<KafkaProducer>>();

            using var producer = new KafkaProducer(kafkaOptions, producerOptions, serializer, logger);

            var messages = Enumerable.Range(1, 10)
                .Select(i => new TestMessage { Id = $"id-{i}", Name = $"Message {i}", Value = i })
                .ToList();

            var results = new List<MessageResult>();
            foreach (var message in messages)
            {
                var result = await producer.ProduceAsync("batch-topic", message, message.Id);
                results.Add(result);
            }

            await producer.FlushAsync();

            results.Should().HaveCount(10);
            results.Should().AllSatisfy(r =>
            {
                r.IsSuccess.Should().BeTrue();
                r.Topic.Should().Be("batch-topic");
            });
        }

        [Fact]
        public async Task Flush_WaitsForPendingMessages()
        {
            var kafkaOptions = Options.Create(new KafkaOptions
            {
                BootstrapServers = _bootstrapServers,
                ClientId = "test-producer"
            });

            var producerOptions = Options.Create(new ProducerOptions
            {
                DefaultTopic = "flush-topic",
                LingerMs = 1000
            });

            var serializer = new JsonMessageSerializer();
            var logger = Mock.Of<ILogger<KafkaProducer>>();

            using var producer = new KafkaProducer(kafkaOptions, producerOptions, serializer, logger);

            var testMessage = new TestMessage { Id = "flush-test", Name = "Flush", Value = 1 };
            await producer.ProduceAsync("flush-topic", testMessage);

            var act = () => producer.Flush(TimeSpan.FromSeconds(5));

            act.Should().NotThrow();
        }

        private record TestMessage
        {
            public string Id { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public int Value { get; init; }
        }
    }
}
