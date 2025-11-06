using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sakin.Agents.Windows.Configuration;
using Sakin.Agents.Windows.Messaging;
using Sakin.Agents.Windows.Services;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices((context, services) =>
    {
        IConfiguration configuration = context.Configuration;

        services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));
        services.Configure<EventLogCollectorOptions>(configuration.GetSection(EventLogCollectorOptions.SectionName));
        services.Configure<EventLogKafkaOptions>(configuration.GetSection(EventLogKafkaOptions.SectionName));
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.Configure<ProducerOptions>(configuration.GetSection(ProducerOptions.SectionName));

        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddSingleton<IEventLogPublisher, EventLogPublisher>();
        services.AddHostedService<EventLogCollectorService>();
    })
    .Build();

host.Run();
