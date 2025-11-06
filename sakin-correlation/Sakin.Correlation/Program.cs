using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sakin.Correlation;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Engine;
using Sakin.Correlation.Parsers;
using Sakin.Correlation.Persistence.DependencyInjection;
using Sakin.Correlation.Services;
using Sakin.Correlation.Validation;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Serialization;
using Sakin.Common.Cache;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .ConfigureServices((context, services) =>
    {
        IConfiguration configuration = context.Configuration;

        // Configure Kafka worker options
        services.Configure<KafkaWorkerOptions>(configuration.GetSection(KafkaWorkerOptions.SectionName));
        services.Configure<RulesOptions>(configuration.GetSection(RulesOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<AggregationOptions>(configuration.GetSection(AggregationOptions.SectionName));

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

        // Add correlation persistence services
        services.AddCorrelationPersistence(configuration);

        // Add Redis client
        services.AddSingleton<IRedisClient, RedisClient>();

        // Add correlation engine services
        services.AddSingleton<IRuleEvaluator, RuleEvaluator>();
        services.AddSingleton<IRuleEvaluatorV2, RuleEvaluatorV2>();
        
        // Add aggregation services
        services.AddSingleton<IRedisStateManager, RedisStateManager>();
        services.AddSingleton<IAggregationEvaluator, AggregationEvaluatorService>();
        
        // Add parser and validation services
        services.AddSingleton<IRuleValidator, RuleValidator>();
        services.AddSingleton<IRuleParser, RuleParser>();
        
        // Add business services
        services.AddSingleton<IRuleLoaderService, RuleLoaderService>();
        services.AddSingleton<IRuleLoaderServiceV2, RuleLoaderServiceV2>();
        services.AddSingleton<IAlertCreatorService, AlertCreatorService>();

        // Add messaging services
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
        
        // Add hosted services (order matters - rule loader should start before worker)
        services.AddHostedService<RuleLoaderService>();
        services.AddHostedService<RuleLoaderServiceV2>();
        services.AddHostedService<RedisCleanupService>();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
