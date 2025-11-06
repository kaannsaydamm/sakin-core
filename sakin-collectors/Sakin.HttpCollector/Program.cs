using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using Sakin.Common.Logging;
using Sakin.HttpCollector.Configuration;
using Sakin.HttpCollector.Models;
using Sakin.HttpCollector.Services;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    TelemetryExtensions.ConfigureSakinSerilog(
        loggerConfiguration,
        context.Configuration,
        context.HostingEnvironment.EnvironmentName);
});

builder.Services.AddSakinTelemetry(builder.Configuration);

builder.Services.Configure<HttpCollectorOptions>(builder.Configuration.GetSection(HttpCollectorOptions.SectionName));
builder.Services.Configure<KafkaPublisherOptions>(builder.Configuration.GetSection(KafkaPublisherOptions.SectionName));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.Configure<ProducerOptions>(builder.Configuration.GetSection(ProducerOptions.SectionName));

var channel = Channel.CreateUnbounded<RawLogEntry>(new UnboundedChannelOptions
{
    SingleReader = true,
    SingleWriter = false
});

builder.Services.AddSingleton(channel.Reader);
builder.Services.AddSingleton(channel.Writer);

builder.Services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
builder.Services.AddSingleton<IMetricsService, MetricsService>();

builder.Services.AddHostedService<HttpEndpointService>();
builder.Services.AddHostedService<KafkaPublisherService>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapPrometheusScrapingEndpoint();
app.MapGet("/healthz", () => Results.Ok("healthy"));

await app.RunAsync();

public partial class Program;
