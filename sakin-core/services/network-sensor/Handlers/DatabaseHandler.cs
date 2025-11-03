using System.Data;
using Microsoft.Extensions.Options;
using Npgsql;
using Sakin.Core.Sensor.Configuration;

namespace Sakin.Core.Sensor.Handlers
{
    public class DatabaseHandler : IDatabaseHandler
    {
        private readonly DatabaseOptions _options;

        public DatabaseHandler(IOptions<DatabaseOptions> options)
        {
            _options = options.Value;
        }

        public NpgsqlConnection? InitDB()
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

                return conn;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while connecting to the database: {ex.Message}");
                return null;
            }
        }

        public async Task SavePacketAsync(NpgsqlConnection dbConnection, string srcIP, string dstIP, string protocol, DateTime timestamp)
        {
            try
            {
                string query = "INSERT INTO \"PacketData\" (\"srcIp\", \"dstIp\", \"protocol\", \"timestamp\") VALUES (@srcIp, @dstIp, @protocol, @timestamp)";
                using var cmd = new NpgsqlCommand(query, dbConnection);
                cmd.Parameters.AddWithValue("srcIp", srcIP);
                cmd.Parameters.AddWithValue("dstIp", dstIP);
                cmd.Parameters.AddWithValue("protocol", protocol);
                cmd.Parameters.AddWithValue("timestamp", timestamp);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving packet data: {ex.Message}");
            }
        }

        public async Task SaveSNIAsync(NpgsqlConnection dbConnection, string sni, string srcIP, string dstIP, string protocol, DateTime timestamp)
        {
            try
            {
                string query = "INSERT INTO \"SniData\" (\"sni\", \"srcIp\", \"dstIp\", \"protocol\", \"timestamp\") VALUES (@sni, @srcIp, @dstIp, @protocol, @timestamp)";
                using var cmd = new NpgsqlCommand(query, dbConnection);
                cmd.Parameters.AddWithValue("sni", sni);
                cmd.Parameters.AddWithValue("srcIp", srcIP);
                cmd.Parameters.AddWithValue("dstIp", dstIP);
                cmd.Parameters.AddWithValue("protocol", protocol);
                cmd.Parameters.AddWithValue("timestamp", timestamp);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving SNI data: {ex.Message}");
            }
        }
    }
}
