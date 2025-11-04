namespace Sakin.Core.Sensor.Configuration;

public class PostgresOptions
{
    public const string SectionName = "Postgres";

    public bool WriteEnabled { get; set; } = true;
}
