using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using Sakin.Agents.Windows.Configuration;
using Sakin.Agents.Windows.Messaging;
using Sakin.Agents.Windows.Services;
using Sakin.Common.DependencyInjection;
using Sakin.Common.Logging;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

builder.Configuration
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

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.Configure<SakinOptions>(builder.Configuration.GetSection(SakinOptions.SectionName));
builder.Services.Configure<EventLogCollectorOptions>(builder.Configuration.GetSection(EventLogCollectorOptions.SectionName));
builder.Services.Configure<EventLogKafkaOptions>(builder.Configuration.GetSection(EventLogKafkaOptions.SectionName));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.Configure<ProducerOptions>(builder.Configuration.GetSection(ProducerOptions.SectionName));

builder.Services.AddSakinCommon(builder.Configuration);

builder.Services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
builder.Services.AddSingleton<IEventLogPublisher, EventLogPublisher>();

builder.Services.AddSingleton<IAgentCommandHandler, AgentCommandHandler>();

builder.Services.AddSingleton<IKafkaConsumer>(provider =>
{
    var agentOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>().Value;
    var kafkaTopics = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SoarKafkaTopics>>().Value;

    var consumerOptions = new ConsumerOptions
    {
        GroupId = $"agent-{agentOptions.AgentId}",
        Topics = new[] { kafkaTopics.AgentCommand },
        EnableAutoCommit = false
    };

    var kafkaOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KafkaOptions>>().Value;
    var serializer = provider.GetRequiredService<IMessageSerializer>();

    return new KafkaConsumer(kafkaOptions, consumerOptions, serializer);
});

builder.Services.AddHostedService<EventLogCollectorService>();
builder.Services.AddHostedService<AgentCommandWorker>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapPrometheusScrapingEndpoint();
app.MapGet("/healthz", () => Results.Ok("healthy"));

await app.RunAsync();

public partial class Program;
