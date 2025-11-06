using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Sakin.Common.Configuration;
using Sakin.Common.DependencyInjection;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Serialization;
using Sakin.ThreatIntelService.Providers;
using Sakin.ThreatIntelService.Services;
using Sakin.ThreatIntelService.Workers;

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
        services.Configure<ThreatIntelOptions>(configuration.GetSection(ThreatIntelOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

        services.AddOptions<ConsumerOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                var consumerGroup = config.GetValue<string>("Kafka:ThreatIntelConsumerGroup");
                options.GroupId = string.IsNullOrWhiteSpace(consumerGroup) ? "threat-intel-worker" : consumerGroup;
                options.EnableAutoCommit = false;
                options.AutoOffsetReset = AutoOffsetReset.Earliest;

                var lookupTopic = config.GetSection(ThreatIntelOptions.SectionName).GetValue<string>("LookupTopic");
                options.Topics = string.IsNullOrWhiteSpace(lookupTopic)
                    ? new[] { "ti-lookup-queue" }
                    : new[] { lookupTopic };
            });

        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();

        services.AddRedisClient();

        services.AddSingleton<IThreatIntelRateLimiter, RedisThreatIntelRateLimiter>();
        services.AddSingleton<IThreatIntelService, ThreatIntelAggregationService>();

        services.AddHttpClient<OtxProvider>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ThreatIntelOptions>>().Value;
                var providerOptions = options.Providers.FirstOrDefault(p => string.Equals(p.Type, "OTX", StringComparison.OrdinalIgnoreCase));
                var baseUrl = providerOptions?.BaseUrl?.TrimEnd('/') ?? "https://otx.alienvault.com/api/v1";
                client.BaseAddress = new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromSeconds(30);

                if (!string.IsNullOrWhiteSpace(providerOptions?.ApiKey))
                {
                    client.DefaultRequestHeaders.Remove("X-OTX-API-KEY");
                    client.DefaultRequestHeaders.Add("X-OTX-API-KEY", providerOptions.ApiKey);
                }
            });
        services.AddTransient<IThreatIntelProvider, OtxProvider>();

        services.AddHttpClient<AbuseIpDbProvider>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ThreatIntelOptions>>().Value;
                var providerOptions = options.Providers.FirstOrDefault(p => string.Equals(p.Type, "AbuseIPDB", StringComparison.OrdinalIgnoreCase));
                var baseUrl = providerOptions?.BaseUrl?.TrimEnd('/') ?? "https://api.abuseipdb.com/api/v2";
                client.BaseAddress = new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.UserAgent.ParseAdd("sakin-threat-intel-worker/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);

                if (!string.IsNullOrWhiteSpace(providerOptions?.ApiKey))
                {
                    client.DefaultRequestHeaders.Remove("Key");
                    client.DefaultRequestHeaders.Add("Key", providerOptions.ApiKey);
                }
            });
        services.AddTransient<IThreatIntelProvider, AbuseIpDbProvider>();

        services.AddHostedService<ThreatIntelWorker>();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConfiguration(context.Configuration.GetSection("Logging"));
        logging.AddConsole();
    })
    .Build();

await host.RunAsync();
