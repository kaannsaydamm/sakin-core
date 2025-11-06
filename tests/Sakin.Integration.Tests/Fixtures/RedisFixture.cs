using System;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Sakin.Integration.Tests.Fixtures;

public class RedisFixture : IAsyncLifetime
{
    private readonly string _connectionString;
    private IConnectionMultiplexer? _connection;

    public IConnectionMultiplexer Connection => _connection ?? throw new InvalidOperationException("RedisFixture not initialized");

    public RedisFixture(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync()
    {
        _connection = await ConnectionMultiplexer.ConnectAsync(_connectionString);
    }

    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.ExecuteAsync(CommandFlags.FireAndForget, "FLUSHDB");
            _connection.Dispose();
        }
    }

    public async Task FlushDbAsync()
    {
        if (_connection == null)
            throw new InvalidOperationException("RedisFixture not initialized");

        var server = _connection.GetServer(_connection.GetEndPoints().First());
        await server.FlushDatabaseAsync();
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        if (_connection == null)
            throw new InvalidOperationException("RedisFixture not initialized");

        var db = _connection.GetDatabase();
        var value = await db.StringGetAsync(key);

        if (!value.HasValue)
            return null;

        return System.Text.Json.JsonSerializer.Deserialize<T>(value.ToString());
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        if (_connection == null)
            throw new InvalidOperationException("RedisFixture not initialized");

        var db = _connection.GetDatabase();
        var serialized = System.Text.Json.JsonSerializer.Serialize(value);
        await db.StringSetAsync(key, serialized, expiry);
    }
}
