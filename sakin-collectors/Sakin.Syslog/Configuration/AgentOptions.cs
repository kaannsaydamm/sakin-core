namespace Sakin.Syslog.Configuration
{
    public class AgentOptions
    {
        public const string SectionName = "Agent";
        
        public string Name { get; set; } = "syslog-collector-01";
        
        public string Hostname { get; set; } = Environment.MachineName;
    }
}