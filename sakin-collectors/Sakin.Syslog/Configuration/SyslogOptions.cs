namespace Sakin.Syslog.Configuration
{
    public class SyslogOptions
    {
        public const string SectionName = "Syslog";
        
        public int UdpPort { get; set; } = 514;
        
        public int TcpPort { get; set; } = 514;
        
        public bool TcpEnabled { get; set; } = false;
        
        public int BufferSize { get; set; } = 65535;
    }
}