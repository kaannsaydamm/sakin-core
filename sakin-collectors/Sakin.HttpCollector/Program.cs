using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sakin.HttpCollector.Configuration;
using Sakin.HttpCollector.Models;
using Sakin.HttpCollector.Services;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        IConfiguration configuration = context.Configuration;

        services.Configure<HttpCollectorOptions>(configuration.GetSection(HttpCollectorOptions.SectionName));
        services.Configure<KafkaPublisherOptions>(configuration.GetSection(KafkaPublisherOptions.SectionName));
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.Configure<ProducerOptions>(configuration.GetSection(ProducerOptions.SectionName));

        var channel = Channel.CreateUnbounded<RawLogEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        services.AddSingleton(channel.Reader);
        services.AddSingleton(channel.Writer);

        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddSingleton<IMetricsService, MetricsService>();

        services.AddHostedService<HttpEndpointService>();
        services.AddHostedService<KafkaPublisherService>();
    })
    .Build();

await host.RunAsync();
