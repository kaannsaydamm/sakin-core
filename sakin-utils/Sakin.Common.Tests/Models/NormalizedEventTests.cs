using Sakin.Common.Models;
using Xunit;

namespace Sakin.Common.Tests.Models
{
    public class NormalizedEventTests
    {
        [Fact]
        public void NormalizedEvent_InitializesWithDefaults()
        {
            var evt = new NormalizedEvent();

            Assert.NotEqual(Guid.Empty, evt.Id);
            Assert.True(evt.Timestamp <= DateTime.UtcNow);
            Assert.Equal(EventType.Unknown, evt.EventType);
            Assert.Equal(Severity.Info, evt.Severity);
            Assert.Equal(string.Empty, evt.SourceIp);
            Assert.Equal(string.Empty, evt.DestinationIp);
            Assert.Equal(Protocol.Unknown, evt.Protocol);
            Assert.NotNull(evt.Metadata);
            Assert.Empty(evt.Metadata);
        }

        [Fact]
        public void NormalizedEvent_CanSetProperties()
        {
            var evt = new NormalizedEvent
            {
                EventType = EventType.NetworkTraffic,
                Severity = Severity.High,
                SourceIp = "192.168.1.1",
                DestinationIp = "192.168.1.2",
                Protocol = Protocol.TCP
            };

            Assert.Equal(EventType.NetworkTraffic, evt.EventType);
            Assert.Equal(Severity.High, evt.Severity);
            Assert.Equal("192.168.1.1", evt.SourceIp);
            Assert.Equal("192.168.1.2", evt.DestinationIp);
            Assert.Equal(Protocol.TCP, evt.Protocol);
        }

        [Fact]
        public void NetworkEvent_InheritsFromNormalizedEvent()
        {
            var evt = new NetworkEvent();

            Assert.IsAssignableFrom<NormalizedEvent>(evt);
            Assert.Equal(EventType.NetworkTraffic, evt.EventType);
        }

        [Fact]
        public void NetworkEvent_HasNetworkSpecificProperties()
        {
            var evt = new NetworkEvent
            {
                BytesSent = 1024,
                BytesReceived = 2048,
                PacketCount = 10,
                Sni = "example.com",
                HttpUrl = "https://example.com/path"
            };

            Assert.Equal(1024, evt.BytesSent);
            Assert.Equal(2048, evt.BytesReceived);
            Assert.Equal(10, evt.PacketCount);
            Assert.Equal("example.com", evt.Sni);
            Assert.Equal("https://example.com/path", evt.HttpUrl);
        }
    }
}
