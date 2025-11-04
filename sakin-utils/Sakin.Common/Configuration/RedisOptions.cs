namespace Sakin.Common.Configuration
{
    public class RedisOptions
    {
        public const string SectionName = "Redis";

        public string ConnectionString { get; set; } = "localhost:6379";
    }
}