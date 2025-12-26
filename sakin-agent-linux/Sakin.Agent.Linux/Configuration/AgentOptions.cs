using System;
using System.Collections.Generic;

namespace Sakin.Agent.Linux.Configuration
{
    public class AgentOptions
    {
        public const string SectionName = "Agent";

        public string AgentId { get; set; } = Environment.MachineName.ToLowerInvariant();
        public string? Hostname { get; set; }
        public bool DryRun { get; set; } = false;
        public List<string> AllowlistedScripts { get; set; } = new();
        public string ScriptsDirectory { get; set; } = "scripts";
        public TimeSpan CommandExpireTime { get; set; } = TimeSpan.FromMinutes(5);

        // Linux-specific options
        public string IptablesPath { get; set; } = "/sbin/iptables";
        public string Ip6tablesPath { get; set; } = "/sbin/ip6tables";
        public string IptablesSavePath { get; set; } = "/sbin/iptables-save";
        public string IptablesRestorePath { get; set; } = "/sbin/iptables-restore";
    }

    public class SakinOptions
    {
        public const string SectionName = "Sakin";

        public string IngestEndpoint { get; set; } = string.Empty;
        public string AgentToken { get; set; } = string.Empty;
        public string AgentName { get; set; } = Environment.MachineName;
    }
}
