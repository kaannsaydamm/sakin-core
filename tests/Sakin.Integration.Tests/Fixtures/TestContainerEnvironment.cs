using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Sakin.Integration.Tests.Fixtures;

public class TestContainerEnvironment : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;
    private readonly KafkaContainer _kafkaContainer;

    public PostgreSqlContainer PostgresContainer => _postgresContainer;
    public RedisContainer RedisContainer => _redisContainer;
    public KafkaContainer KafkaContainer => _kafkaContainer;

    public string PostgresConnectionString => _postgresContainer.GetConnectionString();
    public string RedisEndpoint => _redisContainer.GetConnectionString();
    public string KafkaBootstrapServers => _kafkaContainer.GetBootstrapAddress();

    public TestContainerEnvironment()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("sakin_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.6.0")
            .Build();
    }

    public async Task InitializeAsync()
    {
        var tasks = new List<Task>
        {
            _postgresContainer.StartAsync(),
            _redisContainer.StartAsync(),
            _kafkaContainer.StartAsync()
        };

        await Task.WhenAll(tasks);
    }

    public async Task DisposeAsync()
    {
        var tasks = new List<Task>();

        if (_postgresContainer is not null)
        {
            tasks.Add(_postgresContainer.StopAsync());
        }

        if (_redisContainer is not null)
        {
            tasks.Add(_redisContainer.StopAsync());
        }

        if (_kafkaContainer is not null)
        {
            tasks.Add(_kafkaContainer.StopAsync());
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }
}
