using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Sakin.Integration.Tests.Fixtures;

public class KafkaFixture : IAsyncLifetime
{
    private readonly string _bootstrapServers;
    private IAdminClient? _adminClient;
    private readonly List<string> _createdTopics = new();

    public KafkaFixture(string bootstrapServers)
    {
        _bootstrapServers = bootstrapServers;
    }

    public async Task InitializeAsync()
    {
        _adminClient = new AdminClientBuilder(
            new AdminClientConfig { BootstrapServers = _bootstrapServers }).Build();

        await EnsureTopicsExistAsync();
    }

    public async Task DisposeAsync()
    {
        if (_adminClient != null && _createdTopics.Count > 0)
        {
            try
            {
                await _adminClient.DeleteTopicsAsync(_createdTopics);
            }
            catch
            {
                // Ignore deletion errors
            }

            _adminClient?.Dispose();
        }
    }

    public async Task<IProducer<string, T>> CreateProducerAsync<T>()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _bootstrapServers,
            ClientId = $"test-producer-{Guid.NewGuid()}"
        };

        return new ProducerBuilder<string, T>(config)
            .SetValueSerializer(new JsonSerializer<T>())
            .Build();
    }

    public async Task<IConsumer<string, T>> CreateConsumerAsync<T>(string groupId, string[] topics)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            SessionTimeoutMs = 6000
        };

        var consumer = new ConsumerBuilder<string, T>(config)
            .SetValueDeserializer(new JsonDeserializer<T>())
            .Build();

        consumer.Subscribe(topics);
        return consumer;
    }

    public async Task EnsureTopicExistsAsync(string topicName, int partitions = 1, short replicationFactor = 1)
    {
        if (_adminClient == null)
            throw new InvalidOperationException("KafkaFixture not initialized");

        try
        {
            var metadata = _adminClient.GetMetadata(topicName, TimeSpan.FromSeconds(5));
            if (metadata.Topics.Any(t => t.Topic == topicName))
            {
                return;
            }
        }
        catch (KafkaException)
        {
            // Topic doesn't exist, create it
        }

        var topicSpec = new TopicSpecification
        {
            Name = topicName,
            NumPartitions = partitions,
            ReplicationFactor = replicationFactor
        };

        try
        {
            await _adminClient.CreateTopicsAsync(new[] { topicSpec });
            _createdTopics.Add(topicName);

            // Wait for topic to be created
            await Task.Delay(500);
        }
        catch (CreateTopicsException ex) when (ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // Topic already exists, ignore
        }
    }

    private async Task EnsureTopicsExistAsync()
    {
        var topics = new[]
        {
            "raw-events",
            "normalized-events",
            "alerts",
            "agent-commands",
            "audit-logs"
        };

        foreach (var topic in topics)
        {
            await EnsureTopicExistsAsync(topic);
        }
    }

    public class JsonSerializer<T> : ISerializer<T>
    {
        public byte[] Serialize(T data, SerializationContext context)
        {
            return JsonSerializer.SerializeToUtf8Bytes(data);
        }
    }

    public class JsonDeserializer<T> : IDeserializer<T>
    {
        public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            if (isNull)
                return default!;

            return JsonSerializer.Deserialize<T>(data) ?? throw new JsonException("Failed to deserialize");
        }
    }
}
