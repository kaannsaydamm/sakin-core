using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Sakin.Common.Configuration;

namespace Sakin.Correlation.Persistence;

public class AlertDbContextFactory : IDesignTimeDbContextFactory<AlertDbContext>
{
    public AlertDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();

        var optionsBuilder = new DbContextOptionsBuilder<AlertDbContext>();
        optionsBuilder.UseNpgsql(databaseOptions.GetConnectionString(), npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(AlertDbContext).Assembly.FullName);
        });

        return new AlertDbContext(optionsBuilder.Options);
    }
}
