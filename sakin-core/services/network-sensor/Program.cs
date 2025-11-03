using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sakin.Common.Configuration;
using Sakin.Core.Sensor.Handlers;
using Sakin.Core.Sensor.Services;
using Sakin.Core.Sensor.Utils;

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

                    services.AddSingleton<IDatabaseHandler, DatabaseHandler>();
                    services.AddSingleton<IPackageInspector, PackageInspector>();

                    services.AddHostedService<NetworkSensorService>();
                });
    }
}
