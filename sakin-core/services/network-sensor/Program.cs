using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sakin.Common.Configuration;
using Sakin.Common.DependencyInjection;
using Sakin.Core.Sensor.Configuration;
using Sakin.Core.Sensor.Handlers;
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
                    services.Configure<DatabaseOptions>(
                        context.Configuration.GetSection(DatabaseOptions.SectionName));

                    services.Configure<PostgresOptions>(
                        context.Configuration.GetSection(PostgresOptions.SectionName));

                    services.Configure<KafkaOptions>(
                        context.Configuration.GetSection(KafkaOptions.SectionName));

                    services.Configure<SensorKafkaOptions>(
                        context.Configuration.GetSection(SensorKafkaOptions.SectionName));

                    services.Configure<ProducerOptions>(
                        context.Configuration.GetSection(ProducerOptions.SectionName));

                    services.Configure<Sakin.Common.Configuration.RedisOptions>(
                        context.Configuration.GetSection(Sakin.Common.Configuration.RedisOptions.SectionName));
                    services.AddRedisClient();

                    services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
                    services.AddSingleton<IKafkaProducer, KafkaProducer>();
                    services.AddSingleton<IEventPublisher, EventPublisher>();

                    services.AddSingleton<IDatabaseHandler, DatabaseHandler>();
                    services.AddSingleton<IPackageInspector, PackageInspector>();

                    services.AddHostedService<NetworkSensorService>();
                });
    }
}
