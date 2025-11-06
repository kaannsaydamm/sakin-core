using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace Sakin.Common.Logging
{
    public static class TelemetryExtensions
    {
        public static IServiceCollection AddSakinTelemetry(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<TelemetryOptions>()
                .Bind(configuration.GetSection(TelemetryOptions.SectionName))
                .PostConfigure(options =>
                {
                    if (string.IsNullOrWhiteSpace(options.ServiceName))
                    {
                        options.ServiceName = Assembly.GetEntryAssembly()?.GetName().Name ?? "sakin-service";
                    }

                    if (string.IsNullOrWhiteSpace(options.Environment))
                    {
                        options.Environment = configuration["ASPNETCORE_ENVIRONMENT"]
                            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                            ?? "Development";
                    }

                    options.TraceSamplerProbability = Math.Clamp(options.TraceSamplerProbability, 0d, 1d);
                });

            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });

            services.AddOpenTelemetry()
                .ConfigureResource((sp, resourceBuilder) =>
                {
                    var options = sp.GetRequiredService<IOptions<TelemetryOptions>>().Value;
                    return resourceBuilder
                        .AddService(
                            serviceName: options.ServiceName!,
                            serviceVersion: Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0",
                            serviceInstanceId: Environment.MachineName)
                        .AddAttributes(new KeyValuePair<string, object?>[]
                        {
                            new("deployment.environment", options.Environment),
                            new("service.namespace", "sakin"),
                        });
                })
                .WithTracing((sp, tracerProviderBuilder) =>
                {
                    var options = sp.GetRequiredService<IOptions<TelemetryOptions>>().Value;
                    if (!options.EnableTracing)
                    {
                        return;
                    }

                    tracerProviderBuilder
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddSource(options.ActivitySourceName ?? options.ServiceName!)
                        .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.TraceSamplerProbability)));

                    if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                    {
                        tracerProviderBuilder.AddOtlpExporter(exporterOptions =>
                        {
                            exporterOptions.Endpoint = new Uri(options.OtlpEndpoint);
                            exporterOptions.Protocol = OtlpExportProtocol.Grpc;
                        });
                    }
                })
                .WithMetrics((sp, meterProviderBuilder) =>
                {
                    var options = sp.GetRequiredService<IOptions<TelemetryOptions>>().Value;
                    if (!options.EnableMetrics)
                    {
                        return;
                    }

                    meterProviderBuilder
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddMeter(options.MeterName ?? options.ServiceName!);

                    meterProviderBuilder.AddPrometheusExporter(exporterOptions =>
                    {
                        exporterOptions.ScrapeEndpointPath = options.PrometheusScrapeEndpoint;
                    });
                });

            return services;
        }

        public static void ConfigureSakinSerilog(LoggerConfiguration loggerConfiguration, IConfiguration configuration, string? environmentName = null)
        {
            var options = configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>() ?? new TelemetryOptions();

            if (string.IsNullOrWhiteSpace(options.ServiceName))
            {
                options.ServiceName = Assembly.GetEntryAssembly()?.GetName().Name ?? "sakin-service";
            }

            environmentName ??= options.Environment
                ?? configuration["ASPNETCORE_ENVIRONMENT"]
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? "Development";

            loggerConfiguration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.With(new DefaultLogPropertyEnricher("service", options.ServiceName))
                .Enrich.With(new DefaultLogPropertyEnricher("correlationId", string.Empty))
                .Enrich.With(new DefaultLogPropertyEnricher("userId", string.Empty))
                .Enrich.With(new DefaultLogPropertyEnricher("action", string.Empty));

            if (options.EnableConsoleJson)
            {
                loggerConfiguration.WriteTo.Console(new RenderedCompactJsonFormatter());
            }

            if (options.EnableLogExport && !string.IsNullOrWhiteSpace(options.OtlpEndpoint))
            {
                loggerConfiguration.WriteTo.OpenTelemetry(exporterOptions =>
                {
                    exporterOptions.Endpoint = options.OtlpEndpoint;
                    exporterOptions.Protocol = OtlpProtocol.Grpc;
                    exporterOptions.ResourceAttributes = new Dictionary<string, object?>
                    {
                        ["service.name"] = options.ServiceName,
                        ["service.namespace"] = "sakin",
                        ["deployment.environment"] = environmentName
                    };
                });
            }
        }
    }
}
