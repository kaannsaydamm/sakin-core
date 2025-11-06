using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using Sakin.Common.DependencyInjection;
using Sakin.Common.Logging;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;
using Sakin.SOAR.Services;
using Sakin.SOAR.Workers;
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

builder.Services.AddSakinCommon(builder.Configuration);

builder.Services.Configure<KafkaOptions>(options =>
{
    var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"];
    if (!string.IsNullOrWhiteSpace(bootstrapServers))
    {
        options.BootstrapServers = bootstrapServers.Trim();
    }

    options.ClientId = "sakin-soar-worker";
});

builder.Services.Configure<ConsumerOptions>(options =>
{
    options.GroupId = builder.Configuration["Kafka:ConsumerGroup"] ?? "sakin-soar-group";
    options.Topics = new[] { builder.Configuration["KafkaTopics:AlertActions"] ?? "sakin-alerts-actions" };
    options.EnableAutoCommit = false;
});

builder.Services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
builder.Services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();

builder.Services.AddSingleton<IPlaybookRepository, PlaybookRepository>();
builder.Services.AddSingleton<IPlaybookExecutor, PlaybookExecutor>();
builder.Services.AddSingleton<INotificationService, NotificationService>();
builder.Services.AddSingleton<IAgentCommandDispatcher, AgentCommandDispatcher>();
builder.Services.AddSingleton<IAuditService, AuditService>();

builder.Services.AddSingleton<ISlackNotificationClient, SlackNotificationClient>();
builder.Services.AddSingleton<IEmailNotificationClient, EmailNotificationClient>();
builder.Services.AddSingleton<IJiraNotificationClient, JiraNotificationClient>();

builder.Services.AddHostedService<SoarWorker>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapPrometheusScrapingEndpoint();
app.MapGet("/healthz", () => Results.Ok("healthy"));

await app.RunAsync();

public partial class Program;
