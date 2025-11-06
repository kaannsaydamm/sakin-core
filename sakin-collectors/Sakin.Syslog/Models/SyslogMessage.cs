namespace Sakin.Syslog.Models
{
    public class SyslogMessage
    {
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        
        public string Hostname { get; set; } = string.Empty;
        
        public string Tag { get; set; } = string.Empty;
        
        public string Message { get; set; } = string.Empty;
        
        public int Priority { get; set; }
        
        public int Facility { get; set; }
        
        public int Severity { get; set; }
        
        public string Raw { get; set; } = string.Empty;
        
        public string RemoteEndpoint { get; set; } = string.Empty;
    }
}