namespace Sakin.Core.Sensor.Messaging;

public interface IEventPublisher
{
    Task PublishPacketEventAsync(PacketEventData packetData, CancellationToken cancellationToken = default);
    Task FlushAsync(CancellationToken cancellationToken = default);
}

public class PacketEventData
{
    public string SourceIp { get; set; } = string.Empty;
    public string DestinationIp { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int? SourcePort { get; set; }
    public int? DestinationPort { get; set; }
    public string RawPayload { get; set; } = string.Empty;
    public string? PayloadPreview { get; set; }
    public string? Sni { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
