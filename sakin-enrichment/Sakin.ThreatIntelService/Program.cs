using System.Net.Http.Headers;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using Polly;
using Sakin.Common.Configuration;
using Sakin.Common.DependencyInjection;
using Sakin.Common.Logging;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Serialization;
using Sakin.ThreatIntelService.Providers;
using Sakin.ThreatIntelService.Services;
using Sakin.ThreatIntelService.Workers;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    TelemetryExtensions.ConfigureSakinSerilog(
        loggerConfiguration,
        context.Configuration,
        context.HostingEnvironment.EnvironmentName);
});

builder.Services.AddSakinTelemetry(builder.Configuration);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.Configure<ThreatIntelOptions>(builder.Configuration.GetSection(ThreatIntelOptions.SectionName));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));

builder.Services.AddOptions<ConsumerOptions>()
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

builder.Services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
builder.Services.AddSingleton<IKafkaConsumer, KafkaConsumer>();

builder.Services.AddRedisClient();

builder.Services.AddSingleton<IThreatIntelRateLimiter, RedisThreatIntelRateLimiter>();
builder.Services.AddSingleton<IThreatIntelService, ThreatIntelAggregationService>();

builder.Services.AddHttpClient<OtxProvider>((provider, client) =>
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

builder.Services.AddTransient<IThreatIntelProvider, OtxProvider>();

builder.Services.AddHttpClient<AbuseIpDbProvider>((provider, client) =>
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

builder.Services.AddTransient<IThreatIntelProvider, AbuseIpDbProvider>();
builder.Services.AddHostedService<ThreatIntelWorker>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapPrometheusScrapingEndpoint();
app.MapGet("/healthz", () => Results.Ok("healthy"));

await app.RunAsync();

public partial class Program;
