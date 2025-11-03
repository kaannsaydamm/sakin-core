using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Sakin.Common.Configuration;

namespace Sakin.Common.Database
{
    public class DatabaseConnectionFactory : IDatabaseConnectionFactory
    {
        private readonly DatabaseOptions _options;
        private readonly ILogger<DatabaseConnectionFactory>? _logger;

        public DatabaseConnectionFactory(IOptions<DatabaseOptions> options, ILogger<DatabaseConnectionFactory>? logger = null)
        {
            _options = options.Value;
            _logger = logger;
        }

        public NpgsqlConnection? CreateConnection()
        {
            try
            {
                string connString = _options.GetConnectionString();
                var conn = new NpgsqlConnection(connString);
                conn.Open();

                if (conn.State != ConnectionState.Open)
                {
                    throw new Exception("Unable to connect to database.");
                }

                using (var cmd = new NpgsqlCommand("SET search_path TO public", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                _logger?.LogInformation("Database connection established successfully");
                return conn;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while connecting to the database: {Message}", ex.Message);
                return null;
            }
        }
    }
}
