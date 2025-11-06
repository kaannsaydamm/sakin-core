using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
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
        services.Configure<RiskScoringConfiguration>(configuration.GetSection("RiskScoring"));

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

        // Add memory caching
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 10000;
        });

        // Register Asset Cache Service
        services.AddSingleton<IAssetCacheService, AssetCacheService>();

        // Add correlation engine services
        services.AddSingleton<IRuleEvaluator, RuleEvaluator>();
        services.AddSingleton<IRuleEvaluatorV2, RuleEvaluatorV2>();
        
        // Add aggregation services
        services.AddSingleton<IRedisStateManager, RedisStateManager>();
        services.AddSingleton<IAggregationEvaluator, AggregationEvaluatorService>();
        
        // Add parser and validation services
        services.AddSingleton<IRuleValidator, RuleValidator>();
        services.AddSingleton<IRuleParser, RuleParser>();
        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();
        
        // Add business services
        services.AddSingleton<IRuleLoaderService, RuleLoaderService>();
        services.AddSingleton<IRuleLoaderServiceV2, RuleLoaderServiceV2>();
        services.AddSingleton<IAlertCreatorService, AlertCreatorService>();
        
        // Add risk scoring services
        services.AddSingleton<ITimeOfDayService, TimeOfDayService>();
        services.AddSingleton<IUserRiskProfileService, UserRiskProfileService>();
        services.AddSingleton<IRiskScoringService, RiskScoringService>();
        services.AddSingleton<RiskScoringWorker>();
        services.AddSingleton<UserRiskProfileWorker>();
        
        // Update AlertCreatorService to include risk scoring worker
        services.AddSingleton<IAlertCreatorServiceWithRiskScoring>(provider =>
        {
            var alertCreator = new AlertCreatorService(
                provider.GetRequiredService<IAlertRepository>(),
                provider.GetRequiredService<IAssetCacheService>(),
                provider.GetRequiredService<ILogger<AlertCreatorService>>(),
                provider.GetRequiredService<IMetricsService>(),
                provider.GetRequiredService<RiskScoringWorker>()
            );
            return alertCreator;
        });
        
        // Add metrics service
        services.AddSingleton<IMetricsService, MetricsService>();

        // Add messaging services
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
        
        // Add health checks
        services.AddHealthChecks();
        
        // Add hosted services (order matters - rule loader should start before worker)
        services.AddHostedService<RuleLoaderService>();
        services.AddHostedService<RuleLoaderServiceV2>();
        services.AddHostedService<RedisCleanupService>();
        services.AddHostedService<UserRiskProfileWorker>();
        services.AddHostedService<RiskScoringWorker>();
        services.AddHostedService<Worker>();
    })
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.Configure(app =>
        {
            app.UseRouting();
            app.UseHttpMetrics();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapMetrics();
                endpoints.MapHealthChecks("/health");
            });
        });
        webBuilder.UseUrls("http://0.0.0.0:8080");
    })
    .Build();

// Validate configuration on startup
var configValidator = host.Services.GetRequiredService<IConfigurationValidator>();
var configuration = host.Services.GetRequiredService<IConfiguration>();
configValidator.ValidateConfiguration(configuration);

await host.RunAsync();
