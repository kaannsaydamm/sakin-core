namespace Sakin.Ingest.Configuration
{
    public class IngestOptions
    {
        public const string SectionName = "Ingestion";

        public int BatchSize { get; set; } = 100;
        public int FlushIntervalSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
        public bool EnableDeduplication { get; set; } = true;
        public bool EnableEnrichment { get; set; } = true;
        public List<string> SupportedFormats { get; set; } = new() { "JSON", "Syslog", "CEF" };
        public string InputTopic { get; set; } = "sakin.raw.events";
        public string OutputTopic { get; set; } = "sakin.normalized.events";
        public string ConsumerGroupId { get; set; } = "sakin-ingest-group";
    }
}