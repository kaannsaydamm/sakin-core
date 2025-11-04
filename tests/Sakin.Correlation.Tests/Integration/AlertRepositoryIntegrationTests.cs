using System;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence;
using Sakin.Correlation.Persistence.DependencyInjection;
using Sakin.Correlation.Persistence.Models;
using Sakin.Correlation.Persistence.Repositories;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sakin.Correlation.Tests.Integration;

public class AlertRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private ServiceProvider _serviceProvider = null!;

    public AlertRepositoryIntegrationTests()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("correlation_db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCorrelationPersistence(options =>
        {
            options.UseNpgsql(_postgresContainer.GetConnectionString(), npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AlertDbContext).Assembly.FullName);
            });
        });

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AlertDbContext>();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _serviceProvider.Dispose();
        }

        await _postgresContainer.StopAsync();
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_PersistsAlertWithContext()
    {
        await ResetDatabaseAsync();

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAlertRepository>();

        var alert = new AlertRecord
        {
            RuleId = "failed-login",
            RuleName = "Failed Login Detection",
            Severity = SeverityLevel.High,
            Status = AlertStatus.New,
            TriggeredAt = DateTimeOffset.UtcNow,
            Source = "integration-test",
            Context = new Dictionary<string, object?>
            {
                ["username"] = "alice",
                ["sourceIp"] = "10.0.0.5",
                ["attempts"] = 5,
                ["details"] = new Dictionary<string, object?>
                {
                    ["firstSeen"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"),
                    ["lastSeen"] = DateTimeOffset.UtcNow.ToString("O")
                }
            },
            MatchedConditions = new[]
            {
                "username equals alice",
                "failure_reason equals invalid_password"
            },
            AggregationCount = 5,
            AggregatedValue = 5
        };

        var stored = await repository.CreateAsync(alert);

        stored.Id.Should().NotBeEmpty();
        stored.Severity.Should().Be(SeverityLevel.High);
        stored.Context.Should().ContainKey("username");

        var fetched = await repository.GetByIdAsync(stored.Id);
        fetched.Should().NotBeNull();
        fetched!.RuleId.Should().Be("failed-login");
        fetched.Context.Should().ContainKey("attempts");
        Convert.ToInt32(fetched.Context["attempts"]).Should().Be(5);

        fetched.MatchedConditions.Should().Contain("username equals alice");
        fetched.MatchedConditions.Should().Contain("failure_reason equals invalid_password");

        fetched.Source.Should().Be("integration-test");
    }

    [Fact]
    public async Task GetRecentAlertsAsync_FiltersBySeverityAndTime()
    {
        await ResetDatabaseAsync();

        var now = DateTimeOffset.UtcNow;

        using (var scope = _serviceProvider.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAlertRepository>();

            await repository.CreateAsync(new AlertRecord
            {
                RuleId = "recent-high",
                RuleName = "Recent High Severity",
                Severity = SeverityLevel.High,
                Status = AlertStatus.New,
                TriggeredAt = now.AddMinutes(-2),
                Context = new Dictionary<string, object?> { ["severityScore"] = 0.9 }
            });

            await repository.CreateAsync(new AlertRecord
            {
                RuleId = "older-medium",
                RuleName = "Older Medium Severity",
                Severity = SeverityLevel.Medium,
                Status = AlertStatus.New,
                TriggeredAt = now.AddHours(-2),
                Context = new Dictionary<string, object?> { ["severityScore"] = 0.5 }
            });
        }

        using var verifyScope = _serviceProvider.CreateScope();
        var verificationRepository = verifyScope.ServiceProvider.GetRequiredService<IAlertRepository>();

        var results = await verificationRepository.GetRecentAlertsAsync(now.AddMinutes(-10), SeverityLevel.High);

        results.Should().HaveCount(1);
        results[0].RuleId.Should().Be("recent-high");
        results[0].Severity.Should().Be(SeverityLevel.High);
    }

    [Fact]
    public async Task GetAlertsByRuleAsync_ReturnsMostRecentFirst()
    {
        await ResetDatabaseAsync();

        using (var scope = _serviceProvider.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAlertRepository>();

            await repository.CreateAsync(new AlertRecord
            {
                RuleId = "aggregation-test",
                RuleName = "Aggregation Test",
                Severity = SeverityLevel.Critical,
                Status = AlertStatus.New,
                TriggeredAt = DateTimeOffset.UtcNow.AddMinutes(-15),
                Context = new Dictionary<string, object?> { ["count"] = 10 }
            });

            await repository.CreateAsync(new AlertRecord
            {
                RuleId = "aggregation-test",
                RuleName = "Aggregation Test",
                Severity = SeverityLevel.Critical,
                Status = AlertStatus.New,
                TriggeredAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                Context = new Dictionary<string, object?> { ["count"] = 25 }
            });
        }

        using var verifyScope = _serviceProvider.CreateScope();
        var verificationRepository = verifyScope.ServiceProvider.GetRequiredService<IAlertRepository>();

        var results = await verificationRepository.GetAlertsByRuleAsync("aggregation-test", limit: 5);

        results.Should().HaveCount(2);
        results[0].Context.Should().ContainKey("count");
        Convert.ToInt32(results[0].Context["count"]).Should().Be(25);
        Convert.ToInt32(results[1].Context["count"]).Should().Be(10);
    }

    private async Task ResetDatabaseAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AlertDbContext>();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE public.alerts");
    }
}
