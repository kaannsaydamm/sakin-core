using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sakin.Common.Models;
using Sakin.Correlation;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Engine;
using Sakin.Correlation.Models;
using Sakin.Correlation.Parsers;
using Sakin.Correlation.Persistence;
using Sakin.Correlation.Persistence.DependencyInjection;
using Sakin.Correlation.Persistence.Models;
using Sakin.Correlation.Persistence.Repositories;
using Sakin.Correlation.Services;
using Sakin.Correlation.Validation;
using Sakin.Common.Cache;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Serialization;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace Sakin.Correlation.Tests.Integration;

public class CorrelationE2ETests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;
    private readonly KafkaContainer _kafkaContainer;
    private ServiceProvider _serviceProvider = null!;
    private IHost _workerHost = null!;

    public CorrelationE2ETests()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("correlation_test_db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.5.0")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        await _redisContainer.StartAsync();
        await _kafkaContainer.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        services.AddCorrelationPersistence(options =>
        {
            options.UseNpgsql(_postgresContainer.GetConnectionString(), npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AlertDbContext).Assembly.FullName);
            });
        });

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AlertDbContext>();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_workerHost != null)
        {
            await _workerHost.StopAsync();
            _workerHost.Dispose();
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _serviceProvider?.Dispose();
        }

        await _postgresContainer.StopAsync();
        await _postgresContainer.DisposeAsync();

        await _redisContainer.StopAsync();
        await _redisContainer.DisposeAsync();

        await _kafkaContainer.StopAsync();
        await _kafkaContainer.DisposeAsync();
    }

    [Fact]
    public async Task StatelessRule_TriggersAlert_WhenEventMatches()
    {
        var topic = "test-events-stateless";
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var statelessRuleFile = Path.Combine(Path.GetTempPath(), $"stateless-rule-{Guid.NewGuid()}.json");
        var statelessRule = new
        {
            id = "test-stateless-01",
            name = "Test Stateless Rule",
            description = "Test stateless authentication failure detection",
            enabled = true,
            severity = "high",
            triggers = new[]
            {
                new
                {
                    type = "event",
                    eventType = "authentication",
                    filters = new Dictionary<string, object>
                    {
                        ["source_type"] = "test-source"
                    }
                }
            },
            conditions = new[]
            {
                new
                {
                    field = "Normalized.EventType",
                    @operator = "equals",
                    value = "auth_failure"
                }
            },
            actions = new[]
            {
                new
                {
                    type = "alert",
                    parameters = new Dictionary<string, object>
                    {
                        ["title"] = "Test Auth Failure"
                    }
                }
            },
            metadata = new Dictionary<string, object>
            {
                ["test"] = "stateless"
            }
        };

        await File.WriteAllTextAsync(statelessRuleFile, JsonSerializer.Serialize(statelessRule));

        _workerHost = CreateWorkerHost(statelessRuleFile, topic);
        await _workerHost.StartAsync(cancellationTokenSource.Token);

        await Task.Delay(2000);

        var testEvent = CreateTestEvent("auth_failure", "test-source");
        await PublishEventToKafka(topic, testEvent);

        await Task.Delay(3000);

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAlertRepository>();
        var alerts = await repository.GetAlertsByRuleAsync("test-stateless-01", limit: 10);

        alerts.Should().NotBeEmpty();
        alerts[0].RuleId.Should().Be("test-stateless-01");
        alerts[0].Severity.Should().Be(SeverityLevel.High);

        File.Delete(statelessRuleFile);
    }

    [Fact]
    public async Task AggregationRule_TriggersAlert_WhenThresholdReached()
    {
        var topic = "test-events-aggregation";
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var aggregationRuleFile = Path.Combine(Path.GetTempPath(), $"aggregation-rule-{Guid.NewGuid()}.json");
        var aggregationRule = new
        {
            id = "test-aggregation-01",
            name = "Test Aggregation Rule",
            description = "Test aggregation with threshold",
            enabled = true,
            trigger = new
            {
                source_types = new[] { "test-source" },
                match = new Dictionary<string, object>
                {
                    ["event_type"] = "login_attempt"
                }
            },
            condition = new
            {
                aggregation = new
                {
                    function = "count",
                    field = "Normalized.EventType",
                    group_by = "Normalized.SourceIp",
                    window_seconds = 300
                },
                @operator = "gte",
                value = 5
            },
            severity = "critical",
            actions = new[]
            {
                new
                {
                    type = "alert",
                    parameters = new Dictionary<string, object>
                    {
                        ["title"] = "Aggregation Threshold Reached"
                    }
                }
            },
            metadata = new Dictionary<string, object>
            {
                ["test"] = "aggregation"
            }
        };

        await File.WriteAllTextAsync(aggregationRuleFile, JsonSerializer.Serialize(aggregationRule));

        _workerHost = CreateWorkerHost(aggregationRuleFile, topic);
        await _workerHost.StartAsync(cancellationTokenSource.Token);

        await Task.Delay(2000);

        for (int i = 0; i < 6; i++)
        {
            var testEvent = CreateTestEvent("login_attempt", "test-source", sourceIp: "192.168.1.100");
            await PublishEventToKafka(topic, testEvent);
            await Task.Delay(200);
        }

        await Task.Delay(5000);

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAlertRepository>();
        var alerts = await repository.GetAlertsByRuleAsync("test-aggregation-01", limit: 10);

        alerts.Should().NotBeEmpty();
        alerts[0].RuleId.Should().Be("test-aggregation-01");
        alerts[0].Severity.Should().Be(SeverityLevel.Critical);

        File.Delete(aggregationRuleFile);
    }

    [Fact]
    public async Task MetricsService_TracksProcessingMetrics()
    {
        var metricsService = new MetricsService();

        metricsService.IncrementEventsProcessed();
        metricsService.IncrementRulesEvaluated();
        metricsService.IncrementAlertsCreated();
        metricsService.IncrementRedisOperations();
        metricsService.RecordProcessingLatency(15.5);

        await Task.CompletedTask;
    }

    private IHost CreateWorkerHost(string rulesPath, string topic)
    {
        var ruleDirectory = Path.GetDirectoryName(rulesPath)!;
        
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = _kafkaContainer.GetBootstrapAddress(),
                ["Kafka:Topic"] = topic,
                ["Kafka:ConsumerGroup"] = $"test-group-{Guid.NewGuid()}",
                ["Rules:RulesPath"] = ruleDirectory,
                ["Database:Host"] = _postgresContainer.Hostname,
                ["Database:Port"] = _postgresContainer.GetMappedPublicPort(5432).ToString(),
                ["Database:Database"] = "correlation_test_db",
                ["Database:Username"] = "postgres",
                ["Database:Password"] = "postgres",
                ["Redis:ConnectionString"] = _redisContainer.GetConnectionString(),
                ["Redis:KeyPrefix"] = "test:",
                ["Redis:DefaultTTL"] = "3600",
                ["Aggregation:MaxWindowSize"] = "86400",
                ["Aggregation:CleanupInterval"] = "300"
            })
            .Build();

        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices(services =>
            {
                services.Configure<KafkaWorkerOptions>(config.GetSection("Kafka"));
                services.Configure<RulesOptions>(config.GetSection("Rules"));
                services.Configure<RedisOptions>(config.GetSection("Redis"));
                services.Configure<AggregationOptions>(config.GetSection("Aggregation"));

                services.Configure<KafkaOptions>(options =>
                {
                    options.BootstrapServers = _kafkaContainer.GetBootstrapAddress();
                });

                services.Configure<ConsumerOptions>(options =>
                {
                    options.GroupId = $"test-group-{Guid.NewGuid()}";
                    options.Topics = new[] { topic };
                    options.EnableAutoCommit = true;
                });

                services.AddCorrelationPersistence(options =>
                {
                    options.UseNpgsql(_postgresContainer.GetConnectionString());
                });

                services.AddSingleton<IRedisClient, RedisClient>();
                services.AddSingleton<IRuleEvaluator, RuleEvaluator>();
                services.AddSingleton<IRuleEvaluatorV2, RuleEvaluatorV2>();
                services.AddSingleton<IRedisStateManager, RedisStateManager>();
                services.AddSingleton<IAggregationEvaluator, AggregationEvaluatorService>();
                services.AddSingleton<IRuleValidator, RuleValidator>();
                services.AddSingleton<IRuleParser, RuleParser>();
                services.AddSingleton<IRuleLoaderService, RuleLoaderService>();
                services.AddSingleton<IRuleLoaderServiceV2, RuleLoaderServiceV2>();
                services.AddSingleton<IAlertCreatorService, AlertCreatorService>();
                services.AddSingleton<IMetricsService, MetricsService>();
                services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
                services.AddSingleton<IKafkaConsumer, KafkaConsumer>();

                services.AddHostedService<RuleLoaderService>();
                services.AddHostedService<RuleLoaderServiceV2>();
                services.AddHostedService<Worker>();
            })
            .Build();
    }

    private EventEnvelope CreateTestEvent(string eventType, string sourceType, string sourceIp = "192.168.1.1")
    {
        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            Source = "integration-test",
            SourceType = sourceType,
            ReceivedAt = DateTimeOffset.UtcNow,
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = EventType.AuthenticationAttempt,
                Severity = Severity.Medium,
                SourceIp = sourceIp,
                DestinationIp = "10.0.0.1",
                Protocol = Protocol.TCP,
                Metadata = new Dictionary<string, object>
                {
                    ["event_type"] = eventType,
                    ["source_type"] = sourceType
                }
            },
            Raw = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["test"] = true,
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
            }),
            Enrichment = new Dictionary<string, object>()
        };
    }

    private async Task PublishEventToKafka(string topic, EventEnvelope eventEnvelope)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _kafkaContainer.GetBootstrapAddress(),
            ClientId = "test-producer"
        };

        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

        var message = new Message<string, string>
        {
            Key = eventEnvelope.EventId.ToString(),
            Value = JsonSerializer.Serialize(eventEnvelope)
        };

        await producer.ProduceAsync(topic, message);
        producer.Flush(TimeSpan.FromSeconds(10));
    }
}
