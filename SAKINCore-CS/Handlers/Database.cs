using System;
using System.Data;
using Npgsql;
using System.Threading.Tasks;

namespace SAKINCore.Handlers
{
    public static class DatabaseHandler
    {
        // Veritabanı bağlantısını başlatan fonksiyon
        public static NpgsqlConnection? InitDB()
        {
            try
            {
                // PostgreSQL DSN (Data Source Name)
                string connString = "Host=localhost;Username=postgres;Password=kaan1980;Database=network_db;Port=5432";
                var conn = new NpgsqlConnection(connString);
                conn.Open();

                // Veritabanı bağlantısını kontrol et
                if (conn.State != ConnectionState.Open)
                {
                    throw new Exception("Unable to connect to database.");
                }

                // Veritabanı seçimini yap
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

        // Paketi veritabanına kaydeden fonksiyon
        public static async Task SavePacketAsync(NpgsqlConnection dbConnection, string srcIP, string dstIP, string protocol, DateTime timestamp)
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

        // SNİ'yi veritabanına kaydeden fonksiyon
        public static async Task SaveSNIAsync(NpgsqlConnection dbConnection, string sni, string srcIP, string dstIP, string protocol, DateTime timestamp)
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
