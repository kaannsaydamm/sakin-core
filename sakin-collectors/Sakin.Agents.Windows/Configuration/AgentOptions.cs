using System;
using System.Collections.Generic;

namespace Sakin.Agents.Windows.Configuration
{
    public class AgentOptions
    {
        public const string SectionName = "Agent";

        public string Name { get; set; } = "eventlog-collector";
        public string Hostname { get; set; } = Environment.MachineName;
        
        // SOAR Agent Configuration
        public string AgentId { get; set; } = Environment.MachineName.ToLowerInvariant();
        public bool DryRun { get; set; } = false;
        public List<string> AllowlistedScripts { get; set; } = new();
        public string ScriptsDirectory { get; set; } = "scripts";
        public TimeSpan CommandExpireTime { get; set; } = TimeSpan.FromMinutes(5);
    }

    public class SakinOptions
    {
        public const string SectionName = "Sakin";

        public string IngestEndpoint { get; set; } = string.Empty;
        public string AgentToken { get; set; } = string.Empty;
        public string AgentName { get; set; } = Environment.MachineName;
    }
}
