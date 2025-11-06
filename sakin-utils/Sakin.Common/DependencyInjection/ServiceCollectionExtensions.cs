using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sakin.Common.Cache;
using Sakin.Common.Configuration;
using Sakin.Common.Validation;

namespace Sakin.Common.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRedisClient(this IServiceCollection services)
        {
            services.Configure<RedisOptions>(options => 
            {
                // Default configuration will be overridden by appsettings.json
            });
            
            services.AddSingleton<IRedisClient, RedisClient>();
            
            return services;
        }

        public static IServiceCollection AddRedisClient(this IServiceCollection services, 
            System.Action<RedisOptions>? configureOptions)
        {
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            else
            {
                services.Configure<RedisOptions>(options => 
                {
                    // Default configuration will be overridden by appsettings.json
                });
            }
            
            services.AddSingleton<IRedisClient, RedisClient>();
            
            return services;
        }

        public static IServiceCollection AddSakinCommon(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddRedisClient();
            
            // Configure SOAR options
            services.Configure<SoarOptions>(configuration.GetSection("SOAR"));
            services.Configure<NotificationOptions>(configuration.GetSection("Notifications"));
            services.Configure<AgentOptions>(configuration.GetSection("Agent"));
            services.Configure<SoarKafkaTopics>(configuration.GetSection("KafkaTopics"));
            
            // Register SOAR services
            services.AddSingleton<IPlaybookValidator, PlaybookValidator>();
            
            return services;
        }
    }
}