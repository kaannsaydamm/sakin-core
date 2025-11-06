using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sakin.Syslog.Configuration;
using Sakin.Syslog.Messaging;
using Sakin.Syslog.Services;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        IConfiguration configuration = context.Configuration;

        services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));
        services.Configure<SyslogOptions>(configuration.GetSection(SyslogOptions.SectionName));
        services.Configure<SyslogKafkaOptions>(configuration.GetSection(SyslogKafkaOptions.SectionName));
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.Configure<ProducerOptions>(configuration.GetSection(ProducerOptions.SectionName));

        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddSingleton<ISyslogPublisher, SyslogPublisher>();
        services.AddSingleton<SyslogParser>();
        services.AddHostedService<SyslogListenerService>();
    })
    .Build();

host.Run();