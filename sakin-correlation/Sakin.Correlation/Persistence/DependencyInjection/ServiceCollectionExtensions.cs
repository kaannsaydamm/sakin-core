using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Sakin.Common.Configuration;
using Sakin.Correlation.Persistence.Repositories;

namespace Sakin.Correlation.Persistence.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCorrelationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        services.AddDbContext<AlertDbContext>((provider, options) =>
        {
            var dbOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            options.UseNpgsql(dbOptions.GetConnectionString(), npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AlertDbContext).Assembly.FullName);
            });
        });

        services.AddScoped<IAlertRepository, AlertRepository>();

        return services;
    }

    public static IServiceCollection AddCorrelationPersistence(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        services.AddDbContext<AlertDbContext>(configureDbContext);
        services.AddScoped<IAlertRepository, AlertRepository>();

        return services;
    }
}
