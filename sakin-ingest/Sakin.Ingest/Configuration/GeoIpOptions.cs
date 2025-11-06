namespace Sakin.Ingest.Configuration;

public class GeoIpOptions
{
    public const string SectionName = "GeoIp";

    public bool Enabled { get; set; } = true;

    public string DatabasePath { get; set; } = "/data/GeoLite2-City.mmdb";

    public int CacheTtlSeconds { get; set; } = 3600;

    public int CacheMaxSize { get; set; } = 10000;
}