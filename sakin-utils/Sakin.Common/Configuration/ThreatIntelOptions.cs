using System.Collections.Generic;

namespace Sakin.Common.Configuration
{
    public class ThreatIntelOptions
    {
        public const string SectionName = "ThreatIntel";

        public bool Enabled { get; set; } = true;

        public List<ThreatIntelProviderOptions> Providers { get; set; } = new();

        public int MaliciousScoreThreshold { get; set; } = 80;

        public int MaliciousCacheTtlDays { get; set; } = 7;

        public int CleanCacheTtlHours { get; set; } = 1;

        public int NotFoundCacheTtlHours { get; set; } = 24;

        public string LookupTopic { get; set; } = "ti-lookup-queue";
    }
}
