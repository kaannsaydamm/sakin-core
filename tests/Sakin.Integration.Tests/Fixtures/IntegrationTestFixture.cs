using System;
using System.Threading.Tasks;
using Xunit;

namespace Sakin.Integration.Tests.Fixtures;

[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
}

public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly TestContainerEnvironment _containerEnvironment;
    public KafkaFixture KafkaFixture { get; private set; } = null!;
    public PostgresFixture PostgresFixture { get; private set; } = null!;
    public RedisFixture RedisFixture { get; private set; } = null!;

    public string PostgresConnectionString => PostgresFixture.ConnectionString;
    public string RedisConnectionString => _containerEnvironment.RedisEndpoint;
    public string KafkaBootstrapServers => _containerEnvironment.KafkaBootstrapServers;

    public IntegrationTestFixture()
    {
        _containerEnvironment = new TestContainerEnvironment();
    }

    public async Task InitializeAsync()
    {
        await _containerEnvironment.InitializeAsync();

        KafkaFixture = new KafkaFixture(_containerEnvironment.KafkaBootstrapServers);
        PostgresFixture = new PostgresFixture(_containerEnvironment.PostgresConnectionString);
        RedisFixture = new RedisFixture(_containerEnvironment.RedisEndpoint);

        var tasks = new Task[]
        {
            KafkaFixture.InitializeAsync(),
            PostgresFixture.InitializeAsync(),
            RedisFixture.InitializeAsync()
        };

        await Task.WhenAll(tasks);
    }

    public async Task DisposeAsync()
    {
        var tasks = new Task[]
        {
            KafkaFixture.DisposeAsync(),
            PostgresFixture.DisposeAsync(),
            RedisFixture.DisposeAsync(),
            _containerEnvironment.DisposeAsync()
        };

        await Task.WhenAll(tasks);
    }
}
