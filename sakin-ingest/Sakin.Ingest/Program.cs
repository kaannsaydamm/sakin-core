using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sakin.Common.Configuration;
using Sakin.Common.DependencyInjection;
using Sakin.Ingest.Configuration;
using Sakin.Ingest.Parsers;
using Sakin.Ingest.Services;
using Sakin.Ingest.Workers;
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
        services.Configure<IngestKafkaOptions>(configuration.GetSection(IngestKafkaOptions.SectionName));
        services.Configure<GeoIpOptions>(configuration.GetSection(GeoIpOptions.SectionName));
        services.Configure<ThreatIntelOptions>(configuration.GetSection(ThreatIntelOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

        services.AddOptions<ConsumerOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                var group = config.GetValue<string>("Kafka:ConsumerGroup");
                var rawTopic = config.GetValue<string>("Kafka:RawEventsTopic");

                options.GroupId = string.IsNullOrWhiteSpace(group) ? options.GroupId : group;

                var topicToUse = string.IsNullOrWhiteSpace(rawTopic)
                    ? (options.Topics.Length > 0 ? options.Topics[0] : "raw-events")
                    : rawTopic;

                options.Topics = new[] { topicToUse };
                options.EnableAutoCommit = false;
                options.AutoOffsetReset = AutoOffsetReset.Earliest;
            });

        services.AddOptions<ProducerOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                var normalizedTopic = config.GetValue<string>("Kafka:NormalizedEventsTopic");
                options.DefaultTopic = string.IsNullOrWhiteSpace(normalizedTopic) ? options.DefaultTopic : normalizedTopic;
            });

        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();

        services.AddRedisClient();

        // Add memory caching with size limit
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 10000; // Default cache size limit
        });

        // Register GeoIP service as singleton since DatabaseReader is expensive
        services.AddSingleton<IGeoIpService, GeoIpService>();

        services.AddSingleton<ParserRegistry>(sp =>
        {
            var registry = new ParserRegistry();
            registry.Register(new WindowsEventLogParser());
            registry.Register(new SyslogParser());
            registry.Register(new ApacheAccessLogParser());
            registry.Register(new FortinetLogParser());
            return registry;
        });

        services.AddHostedService<EventIngestWorker>();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConfiguration(context.Configuration.GetSection("Logging"));
        logging.AddConsole();
    })
    .Build();

await host.RunAsync();
