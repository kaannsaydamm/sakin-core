using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sakin.Correlation.Persistence.Context;

namespace Sakin.Integration.Tests.Fixtures;

public class PostgresFixture : IAsyncLifetime
{
    private readonly string _connectionString;
    private NpgsqlConnection? _connection;

    public string ConnectionString => _connectionString;

    public PostgresFixture(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync()
    {
        _connection = new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync();

        // Run migrations
        await RunMigrationsAsync();

        // Seed reference data if needed
        await SeedReferenceDataAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            try
            {
                // Clean up all data
                await CleanupDatabaseAsync();
            }
            finally
            {
                await _connection.CloseAsync();
                _connection.Dispose();
            }
        }
    }

    public async Task CleanupDatabaseAsync()
    {
        if (_connection == null)
            throw new InvalidOperationException("PostgresFixture not initialized");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            TRUNCATE TABLE alert_status_history CASCADE;
            TRUNCATE TABLE alerts CASCADE;
            TRUNCATE TABLE rules CASCADE;
            TRUNCATE TABLE assets CASCADE;
            TRUNCATE TABLE audit_logs CASCADE;
        ";

        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Some tables might not exist yet, ignore
        }
    }

    private async Task RunMigrationsAsync()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CorrelationDbContext>();
        optionsBuilder.UseNpgsql(_connectionString);

        using var context = new CorrelationDbContext(optionsBuilder.Options);
        await context.Database.MigrateAsync();
    }

    private async Task SeedReferenceDataAsync()
    {
        if (_connection == null)
            throw new InvalidOperationException("PostgresFixture not initialized");

        using var cmd = _connection.CreateCommand();

        // Seed some test rules if table is empty
        cmd.CommandText = @"
            INSERT INTO rules (rule_id, rule_name, severity, enabled, rule_definition, created_at, updated_at)
            SELECT 
                'rule-' || gen_random_uuid()::text,
                'Brute Force Detection',
                'High',
                true,
                jsonb_build_object(
                    'type', 'aggregation',
                    'condition', jsonb_build_object(
                        'event_code', 4625
                    ),
                    'aggregation', jsonb_build_object(
                        'count_threshold', 10,
                        'time_window', 300,
                        'group_by', 'source_ip'
                    )
                ),
                NOW(),
                NOW()
            WHERE NOT EXISTS (SELECT 1 FROM rules WHERE rule_name = 'Brute Force Detection')
        ";

        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Ignore if rules table doesn't exist
        }
    }

    public async Task<NpgsqlConnection> GetConnectionAsync()
    {
        if (_connection == null)
            throw new InvalidOperationException("PostgresFixture not initialized");

        return _connection;
    }
}
