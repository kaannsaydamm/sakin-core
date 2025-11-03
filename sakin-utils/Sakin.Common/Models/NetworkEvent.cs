namespace Sakin.Common.Models
{
    public class NetworkEvent : NormalizedEvent
    {
        public NetworkEvent()
        {
            EventType = EventType.NetworkTraffic;
        }

        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public int PacketCount { get; set; }
        public string? Sni { get; set; }
        public string? HttpUrl { get; set; }
        public string? HttpMethod { get; set; }
        public int? HttpStatusCode { get; set; }
        public string? UserAgent { get; set; }
    }
}
