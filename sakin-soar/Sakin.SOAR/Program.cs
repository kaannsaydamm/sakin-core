using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sakin.Common.DependencyInjection;
using Sakin.Messaging.Configuration;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Producer;
using Sakin.Messaging.Serialization;
using Sakin.SOAR.Services;
using Sakin.SOAR.Workers;

namespace Sakin.SOAR;

public class Program
{
    public static void Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureServices((context, services) =>
            {
                IConfiguration configuration = context.Configuration;

                // Configure SOAR and messaging
                services.AddSakinCommon(configuration);
                
                services.Configure<KafkaOptions>(options =>
                {
                    var bootstrapServers = configuration["Kafka:BootstrapServers"];
                    if (!string.IsNullOrWhiteSpace(bootstrapServers))
                    {
                        options.BootstrapServers = bootstrapServers.Trim();
                    }
                    options.ClientId = "sakin-soar-worker";
                });

                services.Configure<ConsumerOptions>(options =>
                {
                    options.GroupId = configuration["Kafka:ConsumerGroup"] ?? "sakin-soar-group";
                    options.Topics = new[] { configuration["KafkaTopics:AlertActions"] ?? "sakin-alerts-actions" };
                    options.EnableAutoCommit = false; // Manual commit for reliability
                });

                // Add messaging services
                services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
                services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
                services.AddSingleton<IKafkaProducer, KafkaProducer>();

                // Add SOAR services
                services.AddSingleton<IPlaybookRepository, PlaybookRepository>();
                services.AddSingleton<IPlaybookExecutor, PlaybookExecutor>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<IAgentCommandDispatcher, AgentCommandDispatcher>();
                services.AddSingleton<IAuditService, AuditService>();

                // Add notification clients
                services.AddSingleton<ISlackNotificationClient, SlackNotificationClient>();
                services.AddSingleton<IEmailNotificationClient, EmailNotificationClient>();
                services.AddSingleton<IJiraNotificationClient, JiraNotificationClient>();

                // Add hosted services
                services.AddHostedService<SoarWorker>();
            })
            .Build();

        host.Run();
    }
}