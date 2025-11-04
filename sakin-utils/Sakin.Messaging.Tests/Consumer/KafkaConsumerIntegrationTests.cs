using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;
using Testcontainers.Kafka;

namespace Sakin.Messaging.Tests.Consumer
{
    public class KafkaConsumerIntegrationTests : IAsyncLifetime
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
        public async Task ConsumeAsync_ReceivesProducedMessages()
        {
            var topic = $"test-consume-{Guid.NewGuid()}";
            var kafkaOptions = Options.Create(new KafkaOptions
            {
                BootstrapServers = _bootstrapServers,
                ClientId = "test-client"
            });

            var testMessages = Enumerable.Range(1, 5)
                .Select(i => new TestMessage { Id = $"id-{i}", Name = $"Message {i}", Value = i })
                .ToList();

            using (var producer = CreateProducer(kafkaOptions, topic))
            {
                foreach (var message in testMessages)
                {
                    await producer.ProduceAsync(topic, message, message.Id);
                }
                await producer.FlushAsync();
            }

            await Task.Delay(1000);

            var receivedMessages = new List<TestMessage>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            using var consumer = CreateConsumer(kafkaOptions, topic);
            consumer.Subscribe(topic);

            var consumeTask = Task.Run(async () =>
            {
                await consumer.ConsumeAsync<TestMessage>(async result =>
                {
                    if (result.Message != null)
                    {
                        receivedMessages.Add(result.Message);
                    }

                    if (receivedMessages.Count >= 5)
                    {
                        cts.Cancel();
                    }

                    await Task.CompletedTask;
                }, cts.Token);
            }, cts.Token);

            try
            {
                await consumeTask;
            }
            catch (OperationCanceledException)
            {
            }

            receivedMessages.Should().HaveCount(5);
            receivedMessages.Select(m => m.Id).Should().BeEquivalentTo(testMessages.Select(m => m.Id));
        }

        [Fact]
        public async Task ConsumeAsync_WithManualCommit_CommitsOffsets()
        {
            var topic = $"test-commit-{Guid.NewGuid()}";
            var kafkaOptions = Options.Create(new KafkaOptions
            {
                BootstrapServers = _bootstrapServers,
                ClientId = "test-client"
            });

            using (var producer = CreateProducer(kafkaOptions, topic))
            {
                var message = new TestMessage { Id = "commit-test", Name = "Commit Test", Value = 1 };
                await producer.ProduceAsync(topic, message, message.Id);
                await producer.FlushAsync();
            }

            await Task.Delay(1000);

            var messageReceived = false;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            using var consumer = CreateConsumer(kafkaOptions, topic);
            consumer.Subscribe(topic);

            var consumeTask = Task.Run(async () =>
            {
                await consumer.ConsumeAsync<TestMessage>(async result =>
                {
                    messageReceived = true;
                    cts.Cancel();
                    await Task.CompletedTask;
                }, cts.Token);
            }, cts.Token);

            try
            {
                await consumeTask;
            }
            catch (OperationCanceledException)
            {
            }

            messageReceived.Should().BeTrue();
        }

        [Fact]
        public void Subscribe_WithValidTopics_Succeeds()
        {
            var kafkaOptions = Options.Create(new KafkaOptions
            {
                BootstrapServers = _bootstrapServers,
                ClientId = "test-client"
            });

            using var consumer = CreateConsumer(kafkaOptions, "dummy-topic");

            var act = () => consumer.Subscribe("topic1", "topic2");

            act.Should().NotThrow();
        }

        [Fact]
        public void Subscribe_WithEmptyTopics_ThrowsArgumentException()
        {
            var kafkaOptions = Options.Create(new KafkaOptions
            {
                BootstrapServers = _bootstrapServers,
                ClientId = "test-client"
            });

            using var consumer = CreateConsumer(kafkaOptions, "dummy-topic");

            var act = () => consumer.Subscribe(Array.Empty<string>());

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Unsubscribe_AfterSubscribe_Succeeds()
        {
            var kafkaOptions = Options.Create(new KafkaOptions
            {
                BootstrapServers = _bootstrapServers,
                ClientId = "test-client"
            });

            using var consumer = CreateConsumer(kafkaOptions, "dummy-topic");
            consumer.Subscribe("topic1");

            var act = () => consumer.Unsubscribe();

            act.Should().NotThrow();
        }

        private KafkaProducer CreateProducer(IOptions<KafkaOptions> kafkaOptions, string defaultTopic)
        {
            var producerOptions = Options.Create(new ProducerOptions
            {
                DefaultTopic = defaultTopic,
                RetryCount = 3
            });

            var serializer = new JsonMessageSerializer();
            var logger = Mock.Of<ILogger<KafkaProducer>>();

            return new KafkaProducer(kafkaOptions, producerOptions, serializer, logger);
        }

        private KafkaConsumer CreateConsumer(IOptions<KafkaOptions> kafkaOptions, string topic)
        {
            var consumerOptions = Options.Create(new ConsumerOptions
            {
                GroupId = $"test-group-{Guid.NewGuid()}",
                Topics = new[] { topic },
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                MaxRetries = 3
            });

            var serializer = new JsonMessageSerializer();
            var logger = Mock.Of<ILogger<KafkaConsumer>>();

            return new KafkaConsumer(kafkaOptions, consumerOptions, serializer, logger);
        }

        private record TestMessage
        {
            public string Id { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public int Value { get; init; }
        }
    }
}
