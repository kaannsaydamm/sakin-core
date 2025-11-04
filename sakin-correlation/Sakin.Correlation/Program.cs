using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Engine;
using Sakin.Correlation.Parsers;
using Sakin.Correlation.Persistence.DependencyInjection;
using Sakin.Correlation.Services.Alerts;
using Sakin.Correlation.Services.Messaging;
using Sakin.Correlation.Services.Metrics;
using Sakin.Correlation.Services.Processing;
using Sakin.Correlation.Services.Rules;
using Sakin.Correlation.Services.State;
using Sakin.Correlation.Validation;
using Sakin.Correlation.Worker;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;
using StackExchange.Redis;

var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Logger = logger;

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console();
        })
        .ConfigureServices((context, services) =>
        {
            var configuration = context.Configuration;

            services.Configure<KafkaSettings>(configuration.GetSection(KafkaSettings.SectionName));
            services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName));
            services.Configure<RulesSettings>(configuration.GetSection(RulesSettings.SectionName));
            services.Configure<EngineSettings>(configuration.GetSection(EngineSettings.SectionName));

            services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
            services.Configure<ProducerOptions>(options =>
            {
                var kafkaSettings = configuration.GetSection(KafkaSettings.SectionName).Get<KafkaSettings>() ?? new KafkaSettings();
                options.DefaultTopic = kafkaSettings.DeadLetterTopic;
            });

            services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
            services.AddSingleton<IKafkaProducer, KafkaProducer>();
            services.AddSingleton<IDeadLetterPublisher, KafkaDeadLetterPublisher>();

            services.AddSingleton<IRuleValidator, RuleValidator>();
            services.AddSingleton<IRuleParser, RuleParser>();
            services.AddSingleton<IRuleEvaluator, RuleEvaluator>();

            services.AddSingleton<IRuleProvider, FileSystemRuleProvider>();
            services.AddSingleton<CorrelationMetrics>();
            services.AddSingleton<IAlertFactory, AlertFactory>();
            services.AddSingleton<IAlertPersistenceService, AlertPersistenceService>();
            services.AddSingleton<IWindowStateStore, RedisWindowStateStore>();
            services.AddSingleton<IRuleExecutionService, RuleExecutionService>();
            services.AddSingleton<IKafkaEventConsumerFactory, KafkaEventConsumerFactory>();
            services.AddSingleton<ICorrelationPipeline, CorrelationPipeline>();

            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var redisOptions = sp.GetRequiredService<IOptions<RedisSettings>>().Value;
                var configurationOptions = ConfigurationOptions.Parse(redisOptions.ConnectionString);
                configurationOptions.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(configurationOptions);
            });

            services.AddCorrelationPersistence(configuration);

            services.AddHostedService<CorrelationWorker>();
        })
        .Build();

    await host.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Correlation engine terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
