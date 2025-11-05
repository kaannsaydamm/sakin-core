using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sakin.Correlation;
using Sakin.Correlation.Configuration;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Serialization;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .ConfigureServices((context, services) =>
    {
        IConfiguration configuration = context.Configuration;

        services.Configure<KafkaWorkerOptions>(configuration.GetSection(KafkaWorkerOptions.SectionName));

        services.Configure<KafkaOptions>(options =>
        {
            var workerOptions = configuration
                .GetSection(KafkaWorkerOptions.SectionName)
                .Get<KafkaWorkerOptions>() ?? new KafkaWorkerOptions();

            options.BootstrapServers = string.IsNullOrWhiteSpace(workerOptions.BootstrapServers)
                ? options.BootstrapServers
                : workerOptions.BootstrapServers.Trim();

            options.ClientId = string.IsNullOrWhiteSpace(workerOptions.ClientId)
                ? options.ClientId
                : workerOptions.ClientId.Trim();
        });

        services.Configure<ConsumerOptions>(options =>
        {
            var workerOptions = configuration
                .GetSection(KafkaWorkerOptions.SectionName)
                .Get<KafkaWorkerOptions>() ?? new KafkaWorkerOptions();

            var consumerGroup = workerOptions.ConsumerGroup?.Trim();
            var topic = workerOptions.Topic?.Trim();

            if (!string.IsNullOrEmpty(consumerGroup))
            {
                options.GroupId = consumerGroup;
            }

            options.Topics = string.IsNullOrEmpty(topic)
                ? Array.Empty<string>()
                : new[] { topic };
            options.EnableAutoCommit = true;
        });

        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
