using Npgsql;

namespace Sakin.Core.Sensor.Handlers
{
    public interface IDatabaseHandler
    {
        NpgsqlConnection? InitDB();
        Task SavePacketAsync(NpgsqlConnection dbConnection, string srcIP, string dstIP, string protocol, DateTime timestamp);
        Task SaveSNIAsync(NpgsqlConnection dbConnection, string sni, string srcIP, string dstIP, string protocol, DateTime timestamp);
    }
}
