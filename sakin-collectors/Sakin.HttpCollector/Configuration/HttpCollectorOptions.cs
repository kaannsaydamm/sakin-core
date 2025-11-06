namespace Sakin.HttpCollector.Configuration;

public class HttpCollectorOptions
{
    public const string SectionName = "HttpCollector";

    public int Port { get; set; } = 8080;
    public string Path { get; set; } = "/api/events";
    public int MaxBodySize { get; set; } = 65536;
    public string[] ValidApiKeys { get; set; } = Array.Empty<string>();
    public bool RequireApiKey { get; set; } = false;
}
