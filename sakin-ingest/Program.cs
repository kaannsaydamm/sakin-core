using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sakin.Common.Configuration;
using Sakin.Ingest.Configuration;
using Sakin.Ingest.Pipelines;
using Sakin.Ingest.Processors;
using Sakin.Ingest.Services;
using Sakin.Ingest.Sinks;
using Sakin.Ingest.Sources;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;

namespace Sakin.Ingest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            await host.RunAsync();
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    var env = context.HostingEnvironment;

                    config.SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    // Configuration
                    services.Configure<DatabaseOptions>(
                        context.Configuration.GetSection(DatabaseOptions.SectionName));
                    services.Configure<KafkaOptions>(
                        context.Configuration.GetSection(KafkaOptions.SectionName));
                    services.Configure<ProducerOptions>(
                        context.Configuration.GetSection($"{KafkaOptions.SectionName}:Producer"));
                    services.Configure<ConsumerOptions>(
                        context.Configuration.GetSection($"{KafkaOptions.SectionName}:Consumer"));
                    services.Configure<IngestOptions>(
                        context.Configuration.GetSection(IngestOptions.SectionName));

                    // Messaging services
                    services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
                    services.AddSingleton<IKafkaProducer, KafkaProducer>();
                    services.AddSingleton<IKafkaConsumer, KafkaConsumer>();

                    // Pipeline services
                    services.AddSingleton<IEventPipeline, EventPipeline>();
                    services.AddSingleton<IEventSource, KafkaEventSource>();
                    services.AddSingleton<IEventSink, KafkaEventSink>();

                    // Event processors
                    services.AddSingleton<IEventProcessor, PacketInspectorProcessor>();

                    // Main service
                    services.AddHostedService<IngestService>();

                    // Health checks
                    services.AddHealthChecks()
                        .AddCheck<IngestService>("ingest-service");
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();

                    var logLevel = context.Configuration.GetValue<string>("Logging:LogLevel:Default") ?? "Information";
                    logging.SetMinimumLevel(Enum.Parse<LogLevel>(logLevel));
                });
    }
}