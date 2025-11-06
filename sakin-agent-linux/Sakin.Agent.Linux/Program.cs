using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sakin.Agent.Linux.Configuration;
using Sakin.Agent.Linux.Services;
using Sakin.Common.DependencyInjection;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;

var host = Host.CreateDefaultBuilder(args)
    .UseSystemd()
    .ConfigureServices((context, services) =>
    {
        IConfiguration configuration = context.Configuration;

        services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.Configure<ProducerOptions>(configuration.GetSection(ProducerOptions.SectionName));

        // Add SOAR configuration
        services.AddSakinCommon(configuration);

        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        
        // Add SOAR agent services
        services.AddSingleton<IAgentCommandHandler, AgentCommandHandler>();
        services.AddSingleton<IKafkaConsumer>(provider =>
        {
            var consumerOptions = new ConsumerOptions
            {
                GroupId = $"agent-{provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>().Value.AgentId}",
                Topics = new[] { provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SoarKafkaTopics>>().Value.AgentCommand },
                EnableAutoCommit = false
            };
            
            var kafkaOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KafkaOptions>>().Value;
            var serializer = provider.GetRequiredService<IMessageSerializer>();
            
            return new KafkaConsumer(kafkaOptions, consumerOptions, serializer);
        });
        
        // Add hosted services
        services.AddHostedService<AgentCommandWorker>();
        services.AddHostedService<AgentStatusWorker>();
    })
    .Build();

host.Run();