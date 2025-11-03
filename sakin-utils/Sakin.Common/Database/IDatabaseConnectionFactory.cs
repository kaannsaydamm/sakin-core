using Npgsql;

namespace Sakin.Common.Database
{
    public interface IDatabaseConnectionFactory
    {
        NpgsqlConnection? CreateConnection();
    }
}
