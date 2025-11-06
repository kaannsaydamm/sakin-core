using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using Sakin.Analytics.BaselineWorker.Services;
using Sakin.Analytics.BaselineWorker.Workers;
using Sakin.Common.Cache;
using Sakin.Common.Configuration;
using Sakin.Common.Logging;
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

builder.Services.Configure<BaselineAggregationOptions>(builder.Configuration.GetSection("BaselineAggregation"));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection("Redis"));
builder.Services.AddSingleton<IRedisClient, RedisClient>();
builder.Services.AddSingleton<IBaselineCalculatorService, BaselineCalculatorService>();
builder.Services.AddHostedService<BaselineCalculationWorker>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapPrometheusScrapingEndpoint();
app.MapGet("/healthz", () => Results.Ok("healthy"));

await app.RunAsync();

public partial class Program;
