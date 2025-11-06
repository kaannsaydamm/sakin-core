using System.Collections.Generic;

namespace Sakin.Common.Configuration
{
    public class ThreatIntelProviderOptions
    {
        public string Type { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;

        public string ApiKey { get; set; } = string.Empty;

        public string? BaseUrl { get; set; }

        public int DailyQuota { get; set; } = 500;

        public Dictionary<string, string> Settings { get; set; } = new();
    }
}
