using Microsoft.Extensions.DependencyInjection;
using Sakin.Common.Cache;
using Sakin.Common.Configuration;

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
    }
}