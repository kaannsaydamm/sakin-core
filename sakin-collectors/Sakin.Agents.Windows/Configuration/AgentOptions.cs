using System;

namespace Sakin.Agents.Windows.Configuration
{
    public class AgentOptions
    {
        public const string SectionName = "Agent";

        public string Name { get; set; } = "eventlog-collector";

        public string Hostname { get; set; } = Environment.MachineName;
    }
}
