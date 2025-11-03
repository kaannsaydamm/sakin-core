namespace Sakin.Core.Sensor.Configuration
{
    public class DatabaseOptions
    {
        public const string SectionName = "Database";

        public string Host { get; set; } = "localhost";
        public string Username { get; set; } = "postgres";
        public string Password { get; set; } = string.Empty;
        public string Database { get; set; } = "network_db";
        public int Port { get; set; } = 5432;

        public string GetConnectionString()
        {
            return $"Host={Host};Username={Username};Password={Password};Database={Database};Port={Port}";
        }
    }
}
