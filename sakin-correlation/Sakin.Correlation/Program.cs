using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using Sakin.Common.Cache;
using Sakin.Common.Configuration;
using Sakin.Common.DependencyInjection;
using Sakin.Common.Logging;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Engine;
using Sakin.Correlation.Parsers;
using Sakin.Correlation.Persistence.DependencyInjection;
using Sakin.Correlation.Persistence.Repositories;
using Sakin.Correlation.Services;
using Sakin.Correlation.Validation;
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

builder.Services.Configure<KafkaWorkerOptions>(builder.Configuration.GetSection(KafkaWorkerOptions.SectionName));
builder.Services.Configure<RulesOptions>(builder.Configuration.GetSection(RulesOptions.SectionName));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<AggregationOptions>(builder.Configuration.GetSection(AggregationOptions.SectionName));
builder.Services.Configure<RiskScoringConfiguration>(builder.Configuration.GetSection("RiskScoring"));
builder.Services.Configure<AlertLifecycleOptions>(builder.Configuration.GetSection(AlertLifecycleOptions.SectionName));
builder.Services.Configure<AnomalyDetectionOptions>(builder.Configuration.GetSection("AnomalyDetection"));

builder.Services.AddSakinCommon(builder.Configuration);

builder.Services.Configure<KafkaOptions>(options =>
{
    var workerOptions = builder.Configuration
        .GetSection(KafkaWorkerOptions.SectionName)
        .Get<KafkaWorkerOptions>() ?? new KafkaWorkerOptions();

    options.BootstrapServers = string.IsNullOrWhiteSpace(workerOptions.BootstrapServers)
        ? options.BootstrapServers
        : workerOptions.BootstrapServers.Trim();

    options.ClientId = string.IsNullOrWhiteSpace(workerOptions.ClientId)
        ? options.ClientId
        : workerOptions.ClientId.Trim();
});

builder.Services.Configure<ConsumerOptions>(options =>
{
    var workerOptions = builder.Configuration
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

builder.Services.AddCorrelationPersistence(builder.Configuration);
builder.Services.AddSingleton<IRedisClient, RedisClient>();

builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 10000;
});

builder.Services.AddSingleton<IAssetCacheService, AssetCacheService>();
builder.Services.AddSingleton<IRuleEvaluator, RuleEvaluator>();
builder.Services.AddSingleton<IRuleEvaluatorV2, RuleEvaluatorV2>();
builder.Services.AddSingleton<IRedisStateManager, RedisStateManager>();
builder.Services.AddSingleton<IAggregationEvaluator, AggregationEvaluatorService>();
builder.Services.AddSingleton<IRuleValidator, RuleValidator>();
builder.Services.AddSingleton<IRuleParser, RuleParser>();
builder.Services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();
builder.Services.AddSingleton<IRuleLoaderService, RuleLoaderService>();
builder.Services.AddSingleton<IRuleLoaderServiceV2, RuleLoaderServiceV2>();
builder.Services.AddSingleton<IAlertCreatorService, AlertCreatorService>();
builder.Services.AddScoped<IAlertDeduplicationService, AlertDeduplicationService>();
builder.Services.AddScoped<IAlertLifecycleService, AlertLifecycleService>();
builder.Services.AddSingleton<ITimeOfDayService, TimeOfDayService>();
builder.Services.AddSingleton<IUserRiskProfileService, UserRiskProfileService>();
builder.Services.AddSingleton<IAnomalyDetectionService, AnomalyDetectionService>();
builder.Services.AddSingleton<IRiskScoringService, RiskScoringService>();
builder.Services.AddSingleton<RiskScoringWorker>();
builder.Services.AddSingleton<UserRiskProfileWorker>();

builder.Services.AddSingleton<IAlertCreatorServiceWithRiskScoring>(provider =>
{
    var alertCreator = new AlertCreatorService(
        provider.GetRequiredService<IAlertRepository>(),
        provider.GetRequiredService<IAssetCacheService>(),
        provider.GetRequiredService<ILogger<AlertCreatorService>>(),
        provider.GetRequiredService<IMetricsService>(),
        provider.GetRequiredService<RiskScoringWorker>(),
        provider.GetService<IAlertActionPublisher>());

    return alertCreator;
});

builder.Services.AddSingleton<IMetricsService, MetricsService>();
builder.Services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
builder.Services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
builder.Services.AddSingleton<IAlertActionPublisher, AlertActionPublisher>();
builder.Services.AddHealthChecks();

builder.Services.AddHostedService<RuleLoaderService>();
builder.Services.AddHostedService<RuleLoaderServiceV2>();
builder.Services.AddHostedService<RedisCleanupService>();
builder.Services.AddHostedService<UserRiskProfileWorker>();
builder.Services.AddHostedService<RiskScoringWorker>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var configValidator = scope.ServiceProvider.GetRequiredService<IConfigurationValidator>();
    configValidator.ValidateConfiguration(app.Configuration);
}

app.UseSerilogRequestLogging();
app.MapPrometheusScrapingEndpoint();
app.MapHealthChecks("/health");

await app.RunAsync();

public partial class Program;
