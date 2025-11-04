using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sakin.Common.Cache;
using Sakin.Common.Configuration;
using Sakin.Common.DependencyInjection;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Services;
using Sakin.Correlation.Workers;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        var env = context.HostingEnvironment;

        config.SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
              .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables();

        if (env.IsDevelopment())
        {
            config.AddUserSecrets<Program>();
        }
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.Configure<CorrelationKafkaOptions>(configuration.GetSection(CorrelationKafkaOptions.SectionName));
        services.Configure<CorrelationPipelineOptions>(configuration.GetSection(CorrelationPipelineOptions.SectionName));
        services.Configure<CorrelationRulesOptions>(configuration.GetSection(CorrelationRulesOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

        services.AddOptions<ConsumerOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                var group = config.GetValue<string>("Kafka:ConsumerGroup");
                var normalizedTopic = config.GetValue<string>("Kafka:NormalizedEventsTopic");

                options.GroupId = string.IsNullOrWhiteSpace(group) ? options.GroupId : group;

                var topicToUse = string.IsNullOrWhiteSpace(normalizedTopic)
                    ? (options.Topics.Length > 0 ? options.Topics[0] : "normalized-events")
                    : normalizedTopic;

                options.Topics = new[] { topicToUse };
                options.EnableAutoCommit = false;
                options.AutoOffsetReset = AutoOffsetReset.Earliest;
            });

        services.AddOptions<ProducerOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                var alertsTopic = config.GetValue<string>("Kafka:AlertsTopic");
                options.DefaultTopic = string.IsNullOrWhiteSpace(alertsTopic) ? options.DefaultTopic : alertsTopic;
            });

        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();

        services.AddRedisClient();
        services.AddSingleton<IStateManager, RedisStateManager>();
        services.AddSingleton<IRuleProvider, RuleProvider>();
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<IAlertPublisher, AlertPublisher>();
        services.AddSingleton<IAlertRepository, LogAlertRepository>();
        services.AddSingleton<CorrelationPipeline>();

        services.AddHostedService<CorrelationWorker>();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConfiguration(context.Configuration.GetSection("Logging"));
        logging.AddConsole();
    })
    .Build();

await host.RunAsync();
