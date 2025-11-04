using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sakin.Common.Configuration;
using Sakin.Core.Sensor.Configuration;
using Sakin.Core.Sensor.Messaging;
using Sakin.Core.Sensor.Services;
using Sakin.Core.Sensor.Utils;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;

namespace Sakin.Core.Sensor
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
                    // Shared options
                    services.Configure<DatabaseOptions>(
                        context.Configuration.GetSection(DatabaseOptions.SectionName));

                    // Messaging options
                    services.Configure<KafkaOptions>(context.Configuration.GetSection(KafkaOptions.SectionName));
                    services.Configure<ProducerOptions>(context.Configuration.GetSection(ProducerOptions.SectionName));

                    // Sensor options
                    services.Configure<SensorOptions>(context.Configuration.GetSection(SensorOptions.SectionName));

                    // Messaging services
                    services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
                    services.AddSingleton<IKafkaProducer, KafkaProducer>();
                    services.AddSingleton<KafkaEventPublisher>();
                    services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<KafkaEventPublisher>());
                    services.AddHostedService(sp => sp.GetRequiredService<KafkaEventPublisher>());

                    // Packet inspector
                    services.AddSingleton<IPackageInspector, PackageInspector>();

                    // Background services
                    services.AddHostedService<NetworkSensorService>();
                });
    }
}
