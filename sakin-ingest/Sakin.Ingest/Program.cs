using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using Sakin.Common.Configuration;
using Sakin.Common.DependencyInjection;
using Sakin.Common.Logging;
using Sakin.Ingest.Configuration;
using Sakin.Ingest.Parsers;
using Sakin.Ingest.Services;
using Sakin.Ingest.Workers;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;
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
builder.Services.Configure<IngestKafkaOptions>(builder.Configuration.GetSection(IngestKafkaOptions.SectionName));
builder.Services.Configure<GeoIpOptions>(builder.Configuration.GetSection(GeoIpOptions.SectionName));
builder.Services.Configure<ThreatIntelOptions>(builder.Configuration.GetSection(ThreatIntelOptions.SectionName));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));

builder.Services.AddOptions<ConsumerOptions>()
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

builder.Services.AddOptions<ProducerOptions>()
    .Configure<IConfiguration>((options, config) =>
    {
        var normalizedTopic = config.GetValue<string>("Kafka:NormalizedEventsTopic");
        options.DefaultTopic = string.IsNullOrWhiteSpace(normalizedTopic) ? options.DefaultTopic : normalizedTopic;
    });

builder.Services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
builder.Services.AddSingleton<IKafkaConsumer, KafkaConsumer>();

builder.Services.AddRedisClient();

builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 10000;
});

builder.Services.AddSingleton<IGeoIpService, GeoIpService>();
builder.Services.AddSingleton<IAssetCacheService, AssetCacheService>();

builder.Services.AddSingleton<ParserRegistry>(sp =>
{
    var registry = new ParserRegistry();
    registry.Register(new WindowsEventLogParser());
    registry.Register(new SyslogParser());
    registry.Register(new ApacheAccessLogParser());
    registry.Register(new FortinetLogParser());
    return registry;
});

builder.Services.AddHostedService<EventIngestWorker>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapPrometheusScrapingEndpoint();
app.MapGet("/healthz", () => Results.Ok("healthy"));

await app.RunAsync();

public partial class Program;
