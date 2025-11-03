namespace Sakin.Common.Models
{
    public class NormalizedEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public EventType EventType { get; set; } = EventType.Unknown;
        public Severity Severity { get; set; } = Severity.Info;
        public string SourceIp { get; set; } = string.Empty;
        public string DestinationIp { get; set; } = string.Empty;
        public int? SourcePort { get; set; }
        public int? DestinationPort { get; set; }
        public Protocol Protocol { get; set; } = Protocol.Unknown;
        public string? Payload { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public string? DeviceName { get; set; }
        public string? SensorId { get; set; }
    }
}
