using System.Net;
using System.Net.Http.Json;
using System.Text;
using Confluent.Kafka;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sakin.Common.Models;
using Sakin.HttpCollector.Configuration;
using Sakin.HttpCollector.Models;
using Sakin.HttpCollector.Services;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;
using System.Threading.Channels;
using Testcontainers.Kafka;
using Xunit;

namespace Sakin.HttpCollector.Tests.Integration;

public class HttpCollectorE2ETests : IAsyncLifetime
{
    private KafkaContainer? _kafkaContainer;
    private string _kafkaBootstrapServers = string.Empty;
    private readonly int _httpPort = 18080;

    public async Task InitializeAsync()
    {
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.5.0")
            .Build();

        await _kafkaContainer.StartAsync();
        _kafkaBootstrapServers = _kafkaContainer.GetBootstrapAddress();
    }

    public async Task DisposeAsync()
    {
        if (_kafkaContainer != null)
        {
            await _kafkaContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task HttpCollector_ShouldAcceptCefMessage_AndPublishToKafka()
    {
        var cefMessage = "CEF:0|Security|IDS|1.0|100|Attack Detected|9|src=192.168.1.100 dst=10.0.0.1";

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://localhost:{_httpPort}") };

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var host = CreateTestHost();
        var hostTask = host.RunAsync(cts.Token);

        await Task.Delay(2000);

        var content = new StringContent(cefMessage, Encoding.UTF8, "text/plain");
        var response = await httpClient.PostAsync("/api/events", content, cts.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        await Task.Delay(2000);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _kafkaBootstrapServers,
            GroupId = "test-consumer",
            AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe("raw-events");

        var consumeResult = consumer.Consume(TimeSpan.FromSeconds(10));

        consumeResult.Should().NotBeNull();
        consumeResult.Message.Value.Should().Contain("CEF:0");
        consumeResult.Message.Value.Should().Contain("cef_string");

        cts.Cancel();

        try
        {
            await hostTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Fact]
    public async Task HttpCollector_ShouldRejectOversizedPayload()
    {
        var largeMessage = new string('X', 70000);

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://localhost:{_httpPort}") };

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var host = CreateTestHost();
        var hostTask = host.RunAsync(cts.Token);

        await Task.Delay(2000);

        var content = new StringContent(largeMessage, Encoding.UTF8, "text/plain");
        var response = await httpClient.PostAsync("/api/events", content, cts.Token);

        response.StatusCode.Should().Be((HttpStatusCode)413);

        cts.Cancel();

        try
        {
            await hostTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Fact]
    public async Task HttpCollector_ShouldRejectEmptyBody()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://localhost:{_httpPort}") };

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var host = CreateTestHost();
        var hostTask = host.RunAsync(cts.Token);

        await Task.Delay(2000);

        var content = new StringContent("", Encoding.UTF8, "text/plain");
        var response = await httpClient.PostAsync("/api/events", content, cts.Token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        cts.Cancel();

        try
        {
            await hostTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private IHost CreateTestHost()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HttpCollector:Port"] = _httpPort.ToString(),
                ["HttpCollector:Path"] = "/api/events",
                ["HttpCollector:MaxBodySize"] = "65536",
                ["HttpCollector:RequireApiKey"] = "false",
                ["Kafka:BootstrapServers"] = _kafkaBootstrapServers,
                ["Kafka:Topic"] = "raw-events",
                ["KafkaOptions:BootstrapServers"] = _kafkaBootstrapServers,
                ["ProducerOptions:LingerMs"] = "10",
                ["ProducerOptions:Acks"] = "all"
            })
            .Build();

        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(builder =>
            {
                builder.AddConfiguration(configuration);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<HttpCollectorOptions>(configuration.GetSection(HttpCollectorOptions.SectionName));
                services.Configure<KafkaPublisherOptions>(configuration.GetSection(KafkaPublisherOptions.SectionName));
                services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
                services.Configure<ProducerOptions>(configuration.GetSection(ProducerOptions.SectionName));

                var channel = Channel.CreateUnbounded<RawLogEntry>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });

                services.AddSingleton(channel.Reader);
                services.AddSingleton(channel.Writer);

                services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
                services.AddSingleton<IKafkaProducer, KafkaProducer>();
                services.AddSingleton<IMetricsService, MetricsService>();

                services.AddHostedService<HttpEndpointService>();
                services.AddHostedService<KafkaPublisherService>();
            })
            .Build();
    }
}
